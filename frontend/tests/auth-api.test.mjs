import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
const source = readFileSync(new URL('../src/features/auth/api.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
let sequence = 0;
async function api() {
  Object.defineProperty(globalThis, 'navigator', { configurable: true, value: { locks: { request: async (_, action) => action() } } });
  globalThis.BroadcastChannel = class { postMessage() {} close() {} };
  return import('data:text/javascript;base64,' + Buffer.from(compiled + '\n// instance ' + sequence++).toString('base64'));
}
const session = { token: 'access-in-memory', expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: 'user-id', email: 'test@example.com' } };
const response = (body, status = 200) => new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
test('parallel restores share one refresh and include CSRF credentials', async () => {
  const auth = await api(); const calls = [];
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); return response(url.endsWith('/csrf') ? { token: 'csrf-token' } : session); };
  const result = await Promise.all([auth.refresh(), auth.refresh(), auth.refresh()]);
  assert.equal(calls.filter(x => x.url.endsWith('/refresh')).length, 1);
  assert.equal(calls[1].options.headers['X-CSRF-TOKEN'], 'csrf-token');
  assert.equal(calls[1].options.credentials, 'same-origin');
  assert.deepEqual(result, [session, session, session]);
});
test('a rejected refresh clears session without retry loop', async () => {
  const auth = await api(); const values = []; auth.subscribe(value => values.push(value));
  globalThis.fetch = async url => response(url.endsWith('/csrf') ? { token: 'csrf' } : { title: 'Expired' }, url.endsWith('/csrf') ? 200 : 401);
  assert.equal(await auth.refresh(), null); assert.deepEqual(values, [null]);
});
test('private request retries only once after refresh', async () => {
  const auth = await api(); const calls = []; const states = []; auth.subscribe(value => states.push(value));
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); if (url.endsWith('/csrf')) return response({ token: 'csrf' });
    if (url.endsWith('/refresh')) return response(session); return response({ title: 'Invalid session' }, 401); };
  await assert.rejects(auth.authenticatedFetch('/api/v1/private'), error => error.status === 401);
  assert.equal(calls.filter(x => x.url === '/api/v1/private').length, 2);
  assert.equal(calls.filter(x => x.url.endsWith('/refresh')).length, 1);
  assert.equal(calls.at(-1).options.headers.get('Authorization'), 'Bearer access-in-memory');
  assert.equal(states.at(-1), null);
});
test('login keeps access token in memory and logout clears it after server success', async () => {
  const auth = await api(); let authorization; const events = []; auth.subscribe(value => events.push(value));
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return response({ token: 'csrf' });
    if (url.endsWith('/login')) return response(session);
    if (url.endsWith('/logout')) return response(null, 204);
    authorization = options.headers.get('Authorization'); return response({ ok: true });
  };
  await auth.login('test@example.com', 'password'); await auth.authenticatedFetch('/api/v1/private');
  assert.equal(authorization, 'Bearer access-in-memory'); await auth.logout();
  await auth.authenticatedFetch('/api/v1/private'); assert.equal(authorization, null); assert.equal(events.at(-1), null);
});
test('field validation errors are preserved for forms', async () => {
  const auth = await api(); globalThis.fetch = async () => response({ title: 'Invalid data', errors: { Email: ['Email không hợp lệ.'] } }, 400);
  await assert.rejects(auth.publicPost('register', {}), error => error.status === 400 && error.problem.errors.Email[0] === 'Email không hợp lệ.');
});
