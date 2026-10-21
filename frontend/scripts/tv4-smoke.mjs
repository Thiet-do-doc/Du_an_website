// End-to-end TV4 smoke tests against the real Auth/Match/Block integration on a local test database.
// Use disposable, confirmed accounts. Never run relationship-mutation tests against real user data.
import assert from 'node:assert/strict';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { randomUUID } from 'node:crypto';

const api = (process.env.API_URL || 'http://localhost:5080').replace(/\/$/, '');
const allowRelationshipMutation = process.env.TV4_TEST_ALLOW_RELATIONSHIP_MUTATION === 'true';
const allowUnmatchMutation = process.env.TV4_TEST_ALLOW_UNMATCH === 'true';
const connections = [];

async function login(alias) {
  const email = process.env[`TV4_EMAIL_${alias}`];
  const password = process.env[`TV4_PASSWORD_${alias}`];
  if (!email || !password) throw new Error(`Set TV4_EMAIL_${alias} and TV4_PASSWORD_${alias} for a confirmed disposable test account.`);
  const csrfResponse = await fetch(`${api}/api/v1/auth/csrf`, { cache: 'no-store' });
  if (!csrfResponse.ok) throw new Error(`Auth CSRF failed for ${alias}: HTTP ${csrfResponse.status}`);
  const csrfBody = await csrfResponse.json();
  const setCookies = typeof csrfResponse.headers.getSetCookie === 'function'
    ? csrfResponse.headers.getSetCookie()
    : [csrfResponse.headers.get('set-cookie')].filter(Boolean);
  const cookie = setCookies.map(x => x.split(';', 1)[0]).join('; ');
  const response = await fetch(`${api}/api/v1/auth/login`, {
    method: 'POST', cache: 'no-store',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfBody.token, ...(cookie ? { Cookie: cookie } : {}) },
    body: JSON.stringify({ email, password })
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok || !data.token || !data.user?.id) {
    throw new Error(`Login ${alias} failed: HTTP ${response.status} ${data.detail || data.title || data.message || ''}`);
  }
  return data;
}

async function request(path, token, options = {}) {
  const headers = new Headers(options.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  return fetch(`${api}${path}`, { ...options, headers, cache: 'no-store' });
}
async function json(path, token, options = {}) {
  const response = await request(path, token, options);
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  assert.ok(response.ok, `${path}: expected success, got HTTP ${response.status} ${JSON.stringify(data)}`);
  return data;
}
async function connect(token) {
  const connection = new HubConnectionBuilder()
    .withUrl(`${api}/hubs/chat`, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 1000, 3000, 10000])
    .build();
  await connection.start();
  connections.push(connection);
  return connection;
}
function expectEvent(connection, eventName, predicate = () => true, timeoutMs = 7000) {
  return new Promise((resolve, reject) => {
    const handler = payload => {
      if (!predicate(payload)) return;
      clearTimeout(timer);
      connection.off(eventName, handler);
      resolve(payload);
    };
    const timer = setTimeout(() => {
      connection.off(eventName, handler);
      reject(new Error(`Timed out waiting for ${eventName}`));
    }, timeoutMs);
    connection.on(eventName, handler);
  });
}
async function expectHubReject(connection, method, ...args) {
  await assert.rejects(() => connection.invoke(method, ...args), undefined, `${method} should be rejected`);
}

try {
  const [a, b, c] = await Promise.all([login('A'), login('B'), login('C')]);
  const anonymous = await request('/api/v1/conversations', null);
  assert.equal(anonymous.status, 401, 'Private conversation API must reject anonymous access');
  const invalid = await request('/api/v1/conversations', 'not.a.valid.jwt');
  assert.equal(invalid.status, 401, 'Private conversation API must reject an invalid JWT');

  const list = await json('/api/v1/conversations?page=1&pageSize=50', a.token);
  const conversation = list.items.find(item => item.partnerId.toLowerCase() === b.user.id.toLowerCase());
  assert.ok(conversation, 'A/B must have one real, active or read-only conversation created by Matching');
  const conversationId = conversation.id;
  const matchId = conversation.matchId;

  const cHistory = await request(`/api/v1/conversations/${conversationId}/messages`, c.token);
  assert.equal(cHistory.status, 404, 'C must not read A/B history');
  const aHub = await connect(a.token);
  const bHub = await connect(b.token);
  const cHub = await connect(c.token);
  await aHub.invoke('JoinConversation', conversationId);
  await bHub.invoke('JoinConversation', conversationId);
  await expectHubReject(cHub, 'JoinConversation', conversationId);
  await expectHubReject(cHub, 'SendMessage', conversationId, 'should be denied', randomUUID());

  const clientMessageId = randomUUID();
  const content = `TV4 smoke ${new Date().toISOString()}`;
  const bMessageEvent = expectEvent(bHub, 'MessageCreated', item => item.clientMessageId === clientMessageId);
  const bNotificationEvent = expectEvent(bHub, 'NotificationCreated', item => item.type === 'newMessage');
  const acknowledged = await aHub.invoke('SendMessage', conversationId, content, clientMessageId);
  const delivered = await bMessageEvent;
  const notificationEvent = await bNotificationEvent;
  assert.equal(delivered.id, acknowledged.id, 'Hub ACK and recipient event must identify the same persisted message');
  assert.equal(notificationEvent.targetId, acknowledged.id, 'newMessage notification must reference the persisted MessageId');

  const retry = await aHub.invoke('SendMessage', conversationId, content, clientMessageId);
  assert.equal(retry.id, acknowledged.id, 'Retry with same clientMessageId must return the original message');
  await expectHubReject(aHub, 'SendMessage', conversationId, `${content} changed`, clientMessageId);

  const history = await json(`/api/v1/conversations/${conversationId}/messages?limit=50`, b.token);
  assert.ok(history.items.some(item => item.id === acknowledged.id), 'Reloaded history must contain the saved message');
  const bNotifications = await json('/api/v1/notifications?page=1&pageSize=50', b.token);
  const notice = bNotifications.items.find(item => item.type === 'newMessage' && item.targetId === acknowledged.id);
  assert.ok(notice, 'Message notification must be persisted');
  assert.equal(notice.conversationId, conversationId);
  const read = await json(`/api/v1/notifications/${notice.id}/read`, b.token, { method: 'PATCH' });
  assert.ok(read.readAt, 'Recipient can mark their notification as read');
  const crossRead = await request(`/api/v1/notifications/${notice.id}/read`, c.token, { method: 'PATCH' });
  assert.equal(crossRead.status, 404, 'Another account cannot mark this notification as read');

  // Verify that an offline recipient can recover persisted messages and notifications after reconnect/reload.
  const previousMessageId = history.items.at(-1).id;
  const previousNotificationId = bNotifications.items[0]?.id;
  await bHub.stop();
  const offlineClientId = randomUUID();
  const offlineMessage = await aHub.invoke('SendMessage', conversationId, 'TV4 offline catch-up', offlineClientId);
  await bHub.start();
  await bHub.invoke('JoinConversation', conversationId);
  const caughtMessages = await json(`/api/v1/conversations/${conversationId}/messages?afterId=${previousMessageId}&limit=50`, b.token);
  assert.ok(caughtMessages.items.some(item => item.id === offlineMessage.id), 'Catch-up must return messages saved while recipient was offline');
  if (previousNotificationId) {
    const caughtNotifications = await json(`/api/v1/notifications?afterId=${previousNotificationId}&pageSize=50`, b.token);
    assert.ok(caughtNotifications.items.some(item => item.targetId === offlineMessage.id), 'Catch-up must return notifications created while recipient was offline');
  }

  if (allowRelationshipMutation) {
    // This mutates match/block state. Only use disposable test accounts and a test database.
    const blocked = await request(`/api/v1/blocks/${b.user.id}`, a.token, { method: 'POST' });
    assert.ok(blocked.ok, `Block endpoint unavailable: HTTP ${blocked.status}`);
    try {
      await expectHubReject(aHub, 'SendMessage', conversationId, 'blocked test', randomUUID());
      const readBlocked = await request(`/api/v1/conversations/${conversationId}/messages`, a.token);
      assert.equal(readBlocked.status, 404, 'Block must hide conversation history per the agreed two-way policy');
    } finally {
      const unblocked = await request(`/api/v1/blocks/${b.user.id}`, a.token, { method: 'DELETE' });
      if (!unblocked.ok) console.warn(`Warning: clean-up unblock returned HTTP ${unblocked.status}; remove block manually in the test DB.`);
    }
  } else {
    console.log('SKIP relationship-mutation tests: set TV4_TEST_ALLOW_RELATIONSHIP_MUTATION=true only with disposable A/B/C accounts.');
  }

  if (allowUnmatchMutation) {
    const unmatch = await request(`/api/v1/matches/${matchId}`, a.token, { method: 'DELETE' });
    assert.ok(unmatch.ok, `Unmatch endpoint unavailable: HTTP ${unmatch.status}`);
    await expectHubReject(aHub, 'SendMessage', conversationId, 'after unmatch test', randomUUID());
    const readonly = await request(`/api/v1/conversations/${conversationId}/messages`, a.token);
    assert.equal(readonly.status, 200, 'Unmatched history remains read-only under the team policy');
  } else {
    console.log('SKIP unmatch mutation test: set TV4_TEST_ALLOW_UNMATCH=true only with disposable A/B accounts.');
  }

  console.log('PASS: real Auth/JWT, anonymous/invalid token, A/B realtime, C authorization, persisted history, idempotency/conflict, recipient-only read, offline catch-up.');
} catch (error) {
  console.error('FAIL TV4 smoke:', error);
  process.exitCode = 1;
} finally {
  const stopped = await Promise.allSettled(connections.map(connection => connection.stop()));
  const failures = stopped.filter(result => result.status === 'rejected');
  if (failures.length) console.warn(`Warning: ${failures.length} SignalR connection(s) did not stop cleanly.`);
}
