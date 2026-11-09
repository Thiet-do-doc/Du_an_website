import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';

const source = readFileSync(new URL('../src/app/navigation.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
}).outputText;
const nav = await import('data:text/javascript;base64,' + Buffer.from(compiled).toString('base64'));

test('Admin sees all standard member tabs plus the report moderation tab', () => {
  const userHrefs = nav.getVisibleTabs(false).map(([href]) => href);
  const adminHrefs = nav.getVisibleTabs(true).map(([href]) => href);
  assert.deepEqual(userHrefs, ['/', '/discover', '/chat', '/notifications', '/wallet', '/profile', '/safety']);
  assert.deepEqual(adminHrefs, [...userHrefs, '/admin']);
});

test('Admin can access every normal feature and /admin without being redirected away', () => {
  const adminAccess = { userId: 'admin-1', admin: true, complete: true };
  for (const path of ['/', '/discover', '/chat', '/notifications', '/wallet', '/profile', '/safety', '/admin', '/account']) {
    assert.equal(nav.resolveRouteDestination(adminAccess, 'admin-1', path, ''), null, path);
    assert.equal(nav.isAppRoutePath(path), true, path);
  }
  assert.equal(nav.resolveRouteDestination(adminAccess, 'admin-1', '/profile', '?user=u-2'), null);
});

test('Regular members with incomplete profiles are still guided to /profile while legacy redirects work', () => {
  const incomplete = { userId: 'u-1', admin: false, complete: false };
  assert.equal(nav.resolveRouteDestination(incomplete, 'u-1', '/', ''), '/profile');
  assert.equal(nav.resolveRouteDestination(incomplete, 'u-1', '/profile', '?user=other'), '/profile');
  assert.equal(nav.resolveRouteDestination(incomplete, 'u-1', '/account', ''), null);
  assert.equal(nav.resolveRouteDestination(incomplete, 'u-1', '/profile', ''), null);

  const complete = { userId: 'u-1', admin: false, complete: true };
  assert.equal(nav.resolveRouteDestination(complete, 'u-1', '/gifts', ''), '/chat');
  assert.equal(nav.resolveRouteDestination(complete, 'u-1', '/blocks', ''), '/safety');
  assert.equal(nav.resolveRouteDestination(complete, 'u-1', '/safety', ''), null);
});
