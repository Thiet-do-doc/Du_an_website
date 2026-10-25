import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
const compile = source => ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const url = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const authUrl = url(compile(readFileSync(new URL('../src/features/auth/api.ts', import.meta.url), 'utf8')));
const source = readFileSync(new URL('../src/features/safety-admin/api/blocks.ts', import.meta.url), 'utf8')
  .replace("'../../auth/api'", JSON.stringify(authUrl));
const auth = await import(authUrl);
const blocks = await import(url(compile(source)));

test('block list and mutations use the shared JWT transport and POST contract', async () => {
  const calls = [];
  auth.publish({ token: 'fake-block-test-token', expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: 'a', email: 'test@example.com' } });
  globalThis.fetch = async (path, init) => {
    calls.push({ path, init });
    return new Response(init.method ? null : JSON.stringify([{ userId: 'b', blockedAt: '2026-10-21T00:00:00Z' }]),
      { status: init.method ? 204 : 200 });
  };
  assert.equal((await blocks.getBlockedUsers())[0].userId, 'b');
  await blocks.blockUser('b'); await blocks.unblockUser('b');
  assert.deepEqual(calls.map(x => x.init.method ?? 'GET'), ['GET', 'POST', 'DELETE']);
  assert.deepEqual(calls.map(x => x.path), ['/api/v1/blocks', '/api/v1/blocks/b', '/api/v1/blocks/b']);
  for (const { init } of calls) assert.equal(init.headers.get('Authorization'), 'Bearer fake-block-test-token');
});
test('permission failures propagate to the UI', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ title: 'Forbidden' }), { status: 403 });
  await assert.rejects(blocks.blockUser('b'), error => error.status === 403);
});
