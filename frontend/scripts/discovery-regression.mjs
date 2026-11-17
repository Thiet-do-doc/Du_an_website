import assert from 'node:assert/strict';
// Run against a local frontend server. API requests are mocked inside the browser.
// Install Playwright or point PLAYWRIGHT_MODULE_URL to its module URL.
const playwright = await import(process.env.PLAYWRIGHT_MODULE_URL ?? 'playwright');
const chromium = playwright.chromium ?? playwright.default?.chromium;
if (!chromium) throw new Error('Không nạp được Chromium từ Playwright.');

const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE });
const origin = process.env.DISCOVERY_BASE_URL ?? 'http://127.0.0.1:5173';
const person = number => ({ userId: `person-${number}`, displayName: `Person ${String(number).padStart(2, '0')}`, age: 25,
  city: 'Thủ Đức', interests: ['Du lịch'], bio: 'Test', photoUrl: null });

async function scenario(name, run) {
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(10000);
  page.on('dialog', dialog => dialog.accept());
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const state = { candidates: Array.from({ length: 45 }, (_, i) => person(i + 1)), removed: new Set(), requests: [], mutations: [],
    likes: [], matches: [], failDiscover: 0, failAction: false, failConnections: false, gate: null };
  await page.route('**/api/v1/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace('/api/v1/', '');
    const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (path === 'auth/csrf') return json({ token: 'test-csrf' });
    if (path === 'auth/refresh') return json({ token: 'test-token', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: 'viewer', email: 'viewer@example.test' } });
    if (path === 'auth/me') return json({ roles: ['User'], userId: 'viewer' });
    if (path === 'profiles/me') return json({ userId: 'viewer', displayName: 'Viewer', isComplete: true, photos: [], interests: [] });
    if (path === 'interests') return json([{ id: 'travel', name: 'Du lịch' }]);
    if (path === 'notifications/unread-count') return json({ count: 0 });
    if (path === 'matches' || path === 'user-likes/received') {
      if (state.failConnections) return json({ title: 'Temporary failure' }, 503);
      return json(path === 'matches' ? state.matches : state.likes);
    }
    if (path === 'discover') {
      state.requests.push(Object.fromEntries(url.searchParams));
      if (state.failDiscover-- > 0) return json({ title: 'Temporary failure' }, 503);
      const pageNumber = Number(url.searchParams.get('page'));
      const pageSize = Number(url.searchParams.get('pageSize'));
      const available = state.candidates.filter(item => !state.removed.has(item.userId));
      const start = (pageNumber - 1) * pageSize;
      return json({ items: available.slice(start, start + pageSize), hasMore: available.length > start + pageSize, page: pageNumber, pageSize });
    }
    if (/^(passes|user-likes|blocks)\//.test(path)) {
      state.mutations.push(path);
      if (state.gate) await state.gate;
      if (state.failAction) return json({ title: 'Action failed' }, 503);
      const id = path.split('/')[1];
      state.removed.add(id);
      const isMatch = path.startsWith('user-likes/') && state.likes.some(item => item.userId === id);
      state.likes = state.likes.filter(item => item.userId !== id);
      if (isMatch) state.matches.push({ id: 'match-1', conversationId: 'conversation-1', otherUserId: id,
        matchedAt: new Date().toISOString(), isActive: true, otherProfile: state.candidates.find(item => item.userId === id) });
      return path.startsWith('user-likes/') ? json({ isMatch }) : route.fulfill({ status: 204 });
    }
    if (path.startsWith('matches/') && request.method() === 'DELETE') {
      state.mutations.push(path);
      state.matches = state.matches.filter(item => item.id !== path.split('/')[1]);
      return route.fulfill({ status: 204 });
    }
    throw new Error(`Unexpected API ${request.method()} ${path}`);
  });
  try {
    await run(page, state);
    assert.deepEqual(errors, []);
    console.log(`PASS ${name}`);
  } finally { await context.close(); }
}

try {
  await scenario('45 candidates across like/pass/block batches, with a failed next-batch request', async (page, state) => {
    await page.goto(origin + '/discover', { waitUntil: 'domcontentloaded' });
    for (let i = 1; i <= 45; i++) {
      await page.locator('.discovery-person-header h2').filter({ hasText: person(i).displayName }).waitFor();
      if (i === 20) state.failDiscover = 1;
      if (i === 20 || i === 40) {
        await page.getByRole('button', { name: 'Chặn hoặc báo cáo hồ sơ' }).click();
        await page.getByRole('menuitem', { name: 'Chặn người này' }).click();
      } else {
        await page.getByRole('button', { name: i % 3 === 0 ? 'Gửi lời thích' : 'Bỏ qua', exact: true }).click();
      }
      if (i === 20) {
        await page.getByRole('heading', { name: 'Chưa tải được hồ sơ.' }).waitFor();
        await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
      }
    }
    await page.getByRole('heading', { name: 'Bạn đã xem hết hồ sơ.' }).waitFor();
    assert.equal(state.removed.size, 45);
    assert.equal(state.mutations.length, 45);
    assert.ok(state.requests.every(request => request.page === '1'));
  });
  await scenario('filter retained on failed initial request and retry', async (page, state) => {
    await page.goto(origin + '/discover', { waitUntil: 'domcontentloaded' });
    await page.locator('.discovery-person-header h2').waitFor();
    state.failDiscover = 1;
    await page.getByLabel(/^Khu vực/).selectOption('Thủ Đức');
    await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
    await page.locator('.discovery-person-header h2').waitFor();
    assert.equal(state.requests.at(-1).city, 'Thủ Đức');
    assert.equal(await page.getByLabel(/^Khu vực/).inputValue(), 'Thủ Đức');
  });
  await scenario('failed action retains candidate and rapid clicks issue one mutation', async (page, state) => {
    await page.goto(origin + '/discover', { waitUntil: 'domcontentloaded' });
    const pass = page.getByRole('button', { name: 'Bỏ qua', exact: true });
    await pass.waitFor();
    state.failAction = true;
    await pass.click();
    await page.getByRole('alert').filter({ hasText: 'Action failed' }).waitFor();
    assert.ok((await page.locator('.discovery-person-header h2').textContent()).includes('Person 01'));
    assert.equal(state.removed.size, 0);
    state.failAction = false;
    let release;
    state.gate = new Promise(resolve => { release = resolve; });
    await pass.evaluate(button => { button.click(); button.click(); });
    await page.waitForFunction(() => document.querySelector('.discovery-actions button').disabled);
    assert.equal(await page.getByLabel(/^Khu vực/).isDisabled(), true);
    release();
    state.gate = null;
    await page.locator('.discovery-person-header h2').filter({ hasText: 'Person 02' }).waitFor();
    assert.equal(state.mutations.length, 2);
    assert.equal(state.removed.size, 1);
  });
  await scenario('match replaces incoming like, unmatch refreshes count, focus reloads external changes', async (page, state) => {
    state.likes = [person(1)];
    await page.goto(origin + '/discover', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: 'Gửi lời thích', exact: true }).click();
    await page.locator('.discovery-person-header h2').filter({ hasText: 'Person 02' }).waitFor();
    await page.getByRole('button', { name: /^Ghép đôi/ }).click();
    await page.getByRole('button', { name: 'Bỏ ghép đôi', exact: true }).waitFor();
    assert.equal(await page.locator('.received-likes li').count(), 0);
    assert.equal(await page.locator('.match-card').count(), 1);
    await page.getByRole('button', { name: 'Bỏ ghép đôi', exact: true }).click();
    await page.getByRole('heading', { name: 'Chưa có lượt ghép đôi.' }).waitFor();
    assert.equal(state.matches.length, 0);
    state.likes = [person(3)];
    await page.evaluate(() => window.dispatchEvent(new Event('focus')));
    await page.locator('.received-likes li').filter({ hasText: 'Person 03' }).waitFor();
  });
  await scenario('connections error has working retry', async (page, state) => {
    state.failConnections = true;
    await page.goto(origin + '/discover', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: /^Ghép đôi/ }).click();
    await page.getByRole('button', { name: 'Thử lại', exact: true }).waitFor();
    state.failConnections = false;
    state.likes = [person(4)];
    await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
    await page.locator('.received-likes li').filter({ hasText: 'Person 04' }).waitFor();
  });
} finally { await browser.close(); }
