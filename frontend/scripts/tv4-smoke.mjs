// End-to-end checks with PostgreSQL + real SignalR (Development environment only).
// Run from frontend after npm install: npm run test:tv4
import { HubConnectionBuilder } from '@microsoft/signalr';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

const api = process.env.API_URL || 'http://localhost:5080';
const response = async (path, token, options = {}) => fetch(api + path, {
  ...options, headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...options.headers },
});
const json = async (path, token, options) => {
  const res = await response(path, token, options);
  assert.ok(res.ok, `${path} => HTTP ${res.status}`);
  return res.json();
};
const getToken = alias => json(`/api/v1/dev/token/${alias}`);
const connect = async token => {
  const conn = new HubConnectionBuilder()
    .withUrl(`${api}/hubs/chat`, { accessTokenFactory: () => token })
    .withAutomaticReconnect().build();
  await conn.start(); return conn;
};
const waitEvent = (conn, name, predicate = () => true) => new Promise((resolve, reject) => {
  const timeout = setTimeout(() => { conn.off(name, handler); reject(new Error(`Timed out: ${name}`)); }, 5000);
  function handler(payload) { if (!predicate(payload)) return; clearTimeout(timeout); conn.off(name, handler); resolve(payload); }
  conn.on(name, handler);
});
const failInvoke = async (conn, ...args) => {
  let failed = false;
  try { await conn.invoke(...args); } catch { failed = true; }
  assert.ok(failed, `${args[0]} should be forbidden`);
};

let aConnection, bConnection, cConnection;
try {
  const a = await getToken('A');
  const b = await getToken('B');
  const c = await getToken('C');
  await response('/api/v1/dev/block/A/B', null, { method: 'DELETE' });
  await response('/api/v1/dev/users/B/status?active=true', null, { method: 'PATCH' });
  const conversations = await json('/api/v1/conversations', a.token);
  assert.ok(conversations.items.length, 'Missing seeded A/B conversation');
  const chatId = conversations.items.find(x => x.partnerId === b.userId)?.id;
  assert.ok(chatId, 'Missing A/B conversation');

  const denied = await response(`/api/v1/conversations/${chatId}/messages`, c.token);
  assert.equal(denied.status, 404, 'C must not read A/B');
  aConnection = await connect(a.token);
  bConnection = await connect(b.token);
  cConnection = await connect(c.token);
  await aConnection.invoke('JoinConversation', chatId);
  await bConnection.invoke('JoinConversation', chatId);
  await failInvoke(cConnection, 'JoinConversation', chatId);
  await failInvoke(cConnection, 'SendMessage', chatId, 'test', randomUUID());

  const clientId = randomUUID();
  const text = `Smoke test ${new Date().toISOString()}`;
  const incoming = waitEvent(bConnection, 'MessageCreated', m => m.clientMessageId === clientId);
  const first = await aConnection.invoke('SendMessage', chatId, text, clientId);
  const pushed = await incoming;
  assert.equal(first.id, pushed.id, 'B did not receive same saved message');
  const duplicate = await aConnection.invoke('SendMessage', chatId, text, clientId);
  assert.equal(first.id, duplicate.id, 'Retry created duplicate message');
  const history = await json(`/api/v1/conversations/${chatId}/messages?afterId=${first.id}`, b.token);
  assert.equal(history.items.length, 0, 'Unexpected messages after newest smoke message');
  const notif = await json('/api/v1/notifications', b.token);
  const newMsg = notif.items.find(x => x.type === 'newMessage' && x.targetId === chatId);
  assert.ok(newMsg, 'Missing persisted newMessage notification');
  const read = await json(`/api/v1/notifications/${newMsg.id}/read`, b.token, { method: 'PATCH' });
  assert.ok(read.readAt, 'Mark-read did not persist');
  const forbiddenNotification = await response(`/api/v1/notifications/${newMsg.id}/read`, c.token, { method: 'PATCH' });
  assert.equal(forbiddenNotification.status, 404, 'C marked B notification');

  await response('/api/v1/dev/block/A/B', null, { method: 'POST' });
  await failInvoke(bConnection, 'SendMessage', chatId, 'blocked', randomUUID());
  const blockedHistory = await response(`/api/v1/conversations/${chatId}/messages`, a.token);
  assert.equal(blockedHistory.status, 404, 'Blocked conversation remained readable');
  await response('/api/v1/dev/block/A/B', null, { method: 'DELETE' });

  // Simulate loss of connection: B disconnects, A sends, B recovers saved message via afterId.
  await bConnection.stop(); bConnection = undefined;
  const missedId = randomUUID();
  const missed = await aConnection.invoke('SendMessage', chatId, 'Tin nhắn sau khi B mất mạng', missedId);
  bConnection = await connect(b.token);
  await bConnection.invoke('JoinConversation', chatId);
  const caughtUp = await json(`/api/v1/conversations/${chatId}/messages?afterId=${first.id}`, b.token);
  assert.ok(caughtUp.items.some(x => x.id === missed.id), 'Reconnect did not recover saved message');

  await response('/api/v1/dev/users/B/status?active=false', null, { method: 'PATCH' });
  await failInvoke(bConnection, 'SendMessage', chatId, 'locked', randomUUID());
  await response('/api/v1/dev/users/B/status?active=true', null, { method: 'PATCH' });

  console.log('PASS: authorization A/B/C, realtime, retry, history, block, lock, notifications, reconnect');
} catch (e) {
  console.error('FAIL:', e);
  process.exitCode = 1;
} finally {
  await Promise.all([aConnection?.stop(), bConnection?.stop(), cConnection?.stop()].filter(Boolean));
}
