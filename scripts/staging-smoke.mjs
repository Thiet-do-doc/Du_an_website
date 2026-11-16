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
const csrfCookie = csrf.headers.getSetCookie().find(s => /^datingapp\.csrf=/i.test(s)) ?? '';
const [csrfCookiePair, ...csrfAttributes] = csrfCookie.split(';').map(part => part.trim());
for (const attribute of ['secure', 'httponly', 'samesite=strict']) {
  assert.ok(csrfAttributes.some(value => value.toLowerCase() === attribute), `Cookie CSRF thiếu ${attribute}`);
}
assert.ok((await csrf.json()).token, 'Thiếu CSRF token');

const foreignOrigin = 'https://foreign-origin.example';
const refreshUrl = new URL('/api/v1/auth/refresh', origin.origin);
const preflight = await fetch(refreshUrl, {
  method: 'OPTIONS', signal: AbortSignal.timeout(15000),
  headers: { Origin: foreignOrigin, 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'X-CSRF-TOKEN' },
});
assert.equal(preflight.headers.get('access-control-allow-origin'), null, 'Origin ngoài không được phép dùng API Auth');
const noCsrf = await fetch(refreshUrl, {
  method: 'POST', signal: AbortSignal.timeout(15000),
  headers: { Origin: foreignOrigin, Cookie: csrfCookiePair },
});
assert.equal(noCsrf.status, 400, 'Refresh thiếu CSRF token phải bị từ chối');
await get('/api/v1/admin/reports', 401);
console.log('PASS HTTPS, API/DB, SPA /admin, JS bundle, cookie CSRF, origin ngoài, refresh thiếu CSRF và admin 401.');
