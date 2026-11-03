import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
const compile = source => ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const url = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const authUrl = url(compile(readFileSync(new URL('../src/features/auth/api.ts', import.meta.url), 'utf8')));
const source = readFileSync(new URL('../src/features/safety-admin/api/reports.ts', import.meta.url), 'utf8')
  .replace("'../../auth/api'", JSON.stringify(authUrl));
const auth = await import(authUrl);
const reports = await import(url(compile(source)));

test('reports use authenticated POST and cannot submit a chosen reporter or review status', async () => {
  auth.publish({ token: 'report-test-token', expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: 'a', email: 'test@example.com' } });
  globalThis.fetch = async (path, init) => {
    assert.equal(path, '/api/v1/reports'); assert.equal(init.method, 'POST');
    assert.equal(init.headers.get('Authorization'), 'Bearer report-test-token');
    assert.equal(init.headers.get('Content-Type'), 'application/json');
    assert.deepEqual(JSON.parse(init.body), { targetType: 'Message', targetId: 'message-id', reason: 'Test', description: 'Report description' });
    return new Response(JSON.stringify({ id: 'report-id', status: 'Pending' }), { status: 201 });
  };
  const response = await reports.createReport({ targetType: 'Message', targetId: ' message-id ', reason: ' Test ',
    description: ' Report description ', reporterUserId: 'forged', status: 'Actioned' });
  assert.equal(response.id, 'report-id'); assert.equal(response.status, 'Pending');
});

test('inaccessible targets and validation failures reach the form as errors', async () => {
  for (const status of [400, 403, 404, 500]) {
    globalThis.fetch = async () => new Response(JSON.stringify({ title: 'Request failed' }), { status });
    await assert.rejects(reports.createReport({ targetType: 'User', targetId: 'id', reason: 'Test', description: 'Report description' }), error => error.status === status);
  }
});
