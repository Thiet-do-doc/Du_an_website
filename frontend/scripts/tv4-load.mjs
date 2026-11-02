// 20 concurrent real SignalR connections (10 sessions for A, 10 for B).
// Measures actual ACK + all-recipient delivery latency. No result is pre-filled or fabricated.
import assert from 'node:assert/strict';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import os from 'node:os';
import { performance } from 'node:perf_hooks';

const api = (process.env.API_URL || 'http://localhost:5080').replace(/\/$/, '');
const connectionCount = Number(process.env.TV4_LOAD_CONNECTIONS || 20);
const messagesWanted = Number(process.env.TV4_LOAD_MESSAGES || 20);
if (connectionCount !== 20) throw new Error('Issue #35 requires exactly 20 simultaneous sessions; keep TV4_LOAD_CONNECTIONS=20.');
if (!Number.isInteger(messagesWanted) || messagesWanted < 20) throw new Error('TV4_LOAD_MESSAGES must be an integer >= 20.');
const liveConnections = [];

async function login(alias) {
  const email = process.env[`TV4_EMAIL_${alias}`];
  const password = process.env[`TV4_PASSWORD_${alias}`];
  if (!email || !password) throw new Error(`Set TV4_EMAIL_${alias} and TV4_PASSWORD_${alias} for a confirmed test user.`);
  const csrfResponse = await fetch(`${api}/api/v1/auth/csrf`, { cache: 'no-store' });
  if (!csrfResponse.ok) throw new Error(`CSRF ${alias}: HTTP ${csrfResponse.status}`);
  const csrf = await csrfResponse.json();
  const cookies = typeof csrfResponse.headers.getSetCookie === 'function'
    ? csrfResponse.headers.getSetCookie() : [csrfResponse.headers.get('set-cookie')].filter(Boolean);
  const cookie = cookies.map(x => x.split(';', 1)[0]).join('; ');
  const response = await fetch(`${api}/api/v1/auth/login`, {
    method: 'POST', cache: 'no-store',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token, ...(cookie ? { Cookie: cookie } : {}) },
    body: JSON.stringify({ email, password })
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok || !data.token || !data.user?.id) throw new Error(`Login ${alias}: HTTP ${response.status} ${data.detail || data.title || ''}`);
  return data;
}
async function json(path, token) {
  const response = await fetch(`${api}${path}`, { headers: { Authorization: `Bearer ${token}` }, cache: 'no-store' });
  const data = await response.json().catch(() => null);
  assert.ok(response.ok, `${path}: HTTP ${response.status} ${JSON.stringify(data)}`);
  return data;
}
async function connect(token) {
  const connection = new HubConnectionBuilder().withUrl(`${api}/hubs/chat`, {
    accessTokenFactory: () => token
  }).withAutomaticReconnect([0, 1000, 3000, 10000]).build();
  await connection.start();
  liveConnections.push(connection);
  return connection;
}
function watchEvent(connection, eventName, clientMessageId, timeoutMs = 10000) {
  let handler;
  let timer;
  let settled = false;
  let resolvePromise;
  let rejectPromise;
  const promise = new Promise((resolve, reject) => { resolvePromise = resolve; rejectPromise = reject; });
  const cleanup = () => { if (timer) clearTimeout(timer); if (handler) connection.off(eventName, handler); };
  handler = message => {
    if (settled || message.clientMessageId !== clientMessageId) return;
    settled = true; cleanup(); resolvePromise(performance.now());
  };
  connection.on(eventName, handler);
  timer = setTimeout(() => {
    if (settled) return;
    settled = true; cleanup(); rejectPromise(new Error(`${eventName} timed out for ${clientMessageId}`));
  }, timeoutMs);
  return { promise, cancel: () => { if (!settled) { settled = true; cleanup(); } } };
}
const percentile = (values, p) => {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  return Number(sorted[Math.min(sorted.length - 1, Math.ceil(p * sorted.length) - 1)].toFixed(2));
};
const stats = values => values.length ? {
  count: values.length,
  minMs: Number(Math.min(...values).toFixed(2)),
  meanMs: Number((values.reduce((sum, value) => sum + value, 0) / values.length).toFixed(2)),
  p50Ms: percentile(values, 0.50), p95Ms: percentile(values, 0.95),
  maxMs: Number(Math.max(...values).toFixed(2))
} : { count: 0, minMs: null, meanMs: null, p50Ms: null, p95Ms: null, maxMs: null };

try {
  const [a, b] = await Promise.all([login('A'), login('B')]);
  const conversations = await json('/api/v1/conversations?page=1&pageSize=50', a.token);
  const conversation = conversations.items.find(item => item.partnerId.toLowerCase() === b.user.id.toLowerCase());
  if (!conversation) throw new Error('No real A/B conversation found. Finish TV3 Match integration before load testing.');

  const aConnections = await Promise.all(Array.from({ length: connectionCount / 2 }, () => connect(a.token)));
  const bConnections = await Promise.all(Array.from({ length: connectionCount / 2 }, () => connect(b.token)));
  await Promise.all([...aConnections, ...bConnections].map(connection =>
    connection.invoke('JoinConversation', conversation.id)));

  const jobs = Array.from({ length: messagesWanted }, (_, i) => {
    const fromA = i % 2 === 0;
    const clients = fromA ? aConnections : bConnections;
    const recipients = fromA ? bConnections : aConnections;
    return { sender: clients[Math.floor(i / 2) % clients.length], recipients,
      direction: fromA ? 'A-to-B' : 'B-to-A', id: randomUUID(), content: `TV4 load ${i + 1} ${new Date().toISOString()}` };
  });
  const measurements = await Promise.all(jobs.map(async job => {
    const watchers = job.recipients.map(connection => watchEvent(connection, 'MessageCreated', job.id));
    const started = performance.now();
    try {
      const ack = await job.sender.invoke('SendMessage', conversation.id, job.content, job.id);
      const ackMs = performance.now() - started;
      const receivedAt = await Promise.all(watchers.map(x => x.promise));
      return { ok: true, direction: job.direction, ackMs, deliveryMs: Math.max(...receivedAt) - started,
        messageId: ack.id };
    } catch (error) {
      watchers.forEach(x => x.cancel());
      return { ok: false, direction: job.direction, error: error instanceof Error ? error.message : String(error) };
    }
  }));

  const passedSamples = measurements.filter(x => x.ok);
  const failures = measurements.filter(x => !x.ok);
  const ackStats = stats(passedSamples.map(x => x.ackMs));
  const deliveryStats = stats(passedSamples.map(x => x.deliveryMs));
  const report = {
    testedAtUtc: new Date().toISOString(),
    environment: { platform: os.platform(), release: os.release(), arch: os.arch(), node: process.version,
      cpuCount: os.cpus().length, totalMemoryBytes: os.totalmem(), apiBase: api },
    setup: { concurrentHubConnections: connectionCount, aSessions: aConnections.length,
      bSessions: bConnections.length, messageAttempts: messagesWanted, conversationId: conversation.id },
    completedMessages: passedSamples.length, failedMessages: failures.length,
    acknowledgementLatencyMs: ackStats, deliveryToAllPeerSessionsLatencyMs: deliveryStats,
    p95GoalUnder2000ms: deliveryStats.p95Ms !== null && deliveryStats.p95Ms < 2000,
    passed: failures.length === 0 && deliveryStats.p95Ms !== null && deliveryStats.p95Ms < 2000,
    failures
  };
  await mkdir('test-results', { recursive: true });
  const reportPath = `test-results/tv4-load-${new Date().toISOString().replace(/[:.]/g, '-')}.json`;
  await writeFile(reportPath, JSON.stringify(report, null, 2) + '\n', 'utf8');
  console.log(JSON.stringify({ reportPath, ...report }, null, 2));
  if (!report.passed) process.exitCode = 1;
} catch (error) {
  console.error('TV4 load setup/run failed:', error);
  process.exitCode = 1;
} finally {
  const stopped = await Promise.allSettled(liveConnections.map(connection => connection.stop()));
  const errors = stopped.filter(result => result.status === 'rejected');
  if (errors.length) console.warn(`Warning: ${errors.length} SignalR connection(s) did not stop cleanly.`);
}
