// Node 24. Only public endpoints; use the A/B/C runbook for authenticated checks.
import assert from 'node:assert/strict';

const origin = new URL(process.argv[2]);
assert.equal(origin.protocol, 'https:', 'Cần URL staging HTTPS');
assert.ok(!origin.username && !origin.password, 'URL không được chứa thông tin đăng nhập');

async function get(path, expected = 200) {
  const response = await fetch(new URL(path, origin.origin), {
    redirect: 'error', signal: AbortSignal.timeout(15000),
  });
  assert.equal(response.status, expected, path);
  return response;
}

assert.equal((await (await get('/healthz')).text()).trim(), 'ok');
for (const path of ['/api/v1/health', '/api/v1/health/database']) {
  const body = await (await get(path)).json();
  assert.ok(body.status, path);
}
const index = await (await get('/')).text();
assert.equal(await (await get('/admin')).text(), index, 'Tải lại /admin phải nhận cùng SPA');
const asset = index.match(/<script[^>]+src="(\/assets\/[^"]+\.js)"/)?.[1];
assert.ok(asset, 'Không thấy JS bundle trong React build');
assert.match((await get(asset)).headers.get('content-type') ?? '', /javascript/);
const csrf = await get('/api/v1/auth/csrf');
assert.ok(csrf.headers.getSetCookie().some(s => /^datingapp\.csrf=/i.test(s) && /;\s*secure\b/i.test(s)), 'Cookie CSRF cần Secure');
assert.ok((await csrf.json()).token, 'Thiếu CSRF token');
await get('/api/v1/admin/reports', 401);
console.log('PASS HTTPS, API/DB, SPA /admin, JS bundle, CSRF Secure và admin 401.');
