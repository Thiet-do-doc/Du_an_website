import assert from 'node:assert/strict';
// Run against the local frontend with Playwright installed or PLAYWRIGHT_MODULE_URL set.
const playwright = await import(process.env.PLAYWRIGHT_MODULE_URL ?? 'playwright');
const chromium = playwright.chromium ?? playwright.default?.chromium;
if (!chromium) throw new Error('Không nạp được Chromium từ Playwright.');
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE });
const origin = process.env.DISCOVERY_BASE_URL ?? 'http://127.0.0.1:5173';
const postId = '11111111-1111-4111-8111-111111111111';
const viewerId = '22222222-2222-4222-8222-222222222222';
const reportId = '33333333-3333-4333-8333-333333333333';
const now = new Date().toISOString();
const notice = i => ({ id: `notice-${i}`, type: 'postLike', targetId: postId, text: `Notice ${i}`, createdAt: now, readAt: null });

async function scenario(name, run) {
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(10000);
  page.on('dialog', dialog => dialog.accept());
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const state = { notices: Array.from({ length: 55 }, (_, i) => notice(i)), calls: [],
    report: { id: reportId, status: 'Pending', targetType: 'Post', targetId: postId, targetUserId: 'author', reporterUserId: viewerId,
      reason: 'Nội dung không phù hợp', description: 'Please review this post', createdAtUtc: now },
    actions: [], content: 'Original post', comments: [{ id: 'comment-1', authorId: viewerId, authorName: 'Viewer', content: 'My comment', createdAtUtc: now, replyCount: 0 }] };
  await page.route('**/api/v1/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname.replace('/api/v1/', '');
    const method = request.method();
    const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    const post = () => ({ id: postId, authorId: viewerId, authorName: 'Viewer', content: state.content, createdAtUtc: now, isMine: true,
      hasPhoto: false, feeling: null, likeCount: 0, isLiked: false, commentCount: state.comments.length, canOpenAuthorProfile: true, authorAvatarId: null });
    if (path === 'auth/csrf') return json({ token: 'csrf' });
    if (path === 'auth/refresh') return json({ token: 'test-token', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: viewerId, email: 'viewer@example.test' } });
    if (path === 'auth/me') return json({ userId: viewerId, roles: ['Admin'] });
    if (path === 'profiles/me') return json({ userId: viewerId, displayName: 'Viewer', isComplete: true, photos: [], interests: [] });
    if (path === 'notifications/unread-count') return json({ count: state.notices.filter(n => !n.readAt).length });
    if (path === 'notifications') {
      const start = Number(url.searchParams.get('cursor') || 0);
      const size = Number(url.searchParams.get('pageSize'));
      return json({ items: state.notices.slice(start, start + size), nextCursor: start + size < state.notices.length ? String(start + size) : null });
    }
    if (path === 'notifications/read-all') { state.notices.forEach(n => { n.readAt = now; }); return json({ updated: state.notices.length }); }
    if (path === 'blocks') return json([]);
    if (path === 'reports/mine') return json({ items: [], hasMore: false });
    if (path === 'reports' && method === 'POST') { state.calls.push(request.postDataJSON()); return json(state.report, 201); }
    if (path === 'admin/reports') return json([state.report]);
    if (path === `admin/reports/${reportId}`) return json(state.report);
    if (path === `admin/reports/${reportId}/actions`) return json(state.actions);
    if (path === `admin/reports/${reportId}/evidence`) return json({ content: 'Saved evidence', photoUrl: null, deleted: false, isHidden: false });
    if (path === `admin/reports/${reportId}/hide-post`) {
      state.calls.push(request.postDataJSON()); state.report.status = 'Actioned';
      state.actions.push({ id: 'audit-1', action: 'PostHidden', actorUserId: viewerId, reason: request.postDataJSON().reason, createdAtUtc: now });
      return json(state.report);
    }
    if (path === 'posts') return json({ items: [post()], hasMore: false });
    if (path === `posts/${postId}`) {
      if (method === 'PATCH') { state.content = request.postDataJSON().content; state.calls.push(request.postDataJSON()); }
      return json(post());
    }
    if (path === `posts/${postId}/comments`) return json({ items: state.comments, hasMore: false });
    if (path === `posts/${postId}/comments/comment-1` && method === 'DELETE') { state.comments = []; return route.fulfill({ status: 204 }); }
    throw new Error(`Unexpected API ${method} ${path}`);
  });
  try { await run(page, state); assert.deepEqual(errors, []); console.log(`PASS ${name}`); }
  catch (error) { console.error(await page.locator('body').innerText(), state.calls, errors); throw error; }
  finally { await context.close(); }
}
try {
  await scenario('notifications refresh all loaded pages, remove invisible entries and mark all read', async (page, state) => {
    await page.goto(origin + '/notifications', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: 'Xem thông báo trước' }).click();
    await page.locator('.notification-list li').filter({ hasText: 'Notice 54' }).waitFor();
    state.notices = [notice(100), ...state.notices.filter(n => n.id !== 'notice-1')];
    await page.evaluate(() => window.dispatchEvent(new Event('notifications-read')));
    await page.getByRole('link', { name: 'Notice 100', exact: true }).waitFor();
    assert.equal(await page.locator('.notification-list li').count(), 55);
    assert.equal(await page.getByRole('link', { name: 'Notice 1', exact: true }).count(), 0);
    await page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }).click();
    await page.waitForFunction(() => !document.querySelector('.notification-badge'));
    assert.equal(await page.locator('.notification-list li.unread').count(), 0);
    assert.ok(state.notices.every(n => n.readAt));
  });
  await scenario('post report is prefilled and submits the selected post', async (page, state) => {
    await page.goto(origin + '/safety?post=' + postId, { waitUntil: 'domcontentloaded' });
    await page.getByLabel(/^Lý do/).selectOption('Nội dung không phù hợp');
    await page.getByLabel('Mô tả', { exact: true }).fill('Bài viết có nội dung không phù hợp.');
    await page.locator('form').getByRole('button', { name: 'Gửi báo cáo', exact: true }).click();
    await page.getByText('Đã gửi báo cáo. Nhóm kiểm duyệt sẽ xem xét.', { exact: true }).waitFor();
    assert.equal(state.calls[0].targetType, 'Post'); assert.equal(state.calls[0].targetId, postId);
    assert.equal(await page.getByRole('button', { name: 'Chặn bằng ID' }).count(), 0);
  });
  await scenario('admin reads evidence and hides post with audit', async (page, state) => {
    await page.goto(origin + '/admin', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: /Bài viết · Nội dung không phù hợp/ }).click();
    await page.getByRole('button', { name: 'Xem bài viết được báo cáo' }).click();
    await page.getByText('Saved evidence', { exact: true }).waitFor();
    await page.getByLabel('Lý do xử lý', { exact: true }).fill('Nội dung vi phạm đã được kiểm tra');
    await page.getByRole('button', { name: 'Ẩn bài viết và xử lý báo cáo' }).click();
    await page.getByText('Đã ẩn bài viết và ghi nhận kết quả xử lý.', { exact: true }).waitFor();
    assert.equal(state.report.status, 'Actioned'); assert.equal(state.actions[0].action, 'PostHidden');
  });
  await scenario('author edits a post and removes own comment', async (page, state) => {
    await page.goto(origin, { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: 'Sửa bài', exact: true }).click();
    await page.locator('.post-card').getByLabel('Nội dung bài viết', { exact: true }).fill('Edited post');
    await page.getByRole('button', { name: 'Lưu chỉnh sửa', exact: true }).click();
    await page.locator('.post-body').filter({ hasText: 'Edited post' }).waitFor();
    await page.getByRole('button', { name: /Bình luận ·/ }).click();
    await page.getByRole('button', { name: 'Xóa bình luận', exact: true }).click();
    await page.getByText('Chưa có bình luận.', { exact: true }).waitFor();
    assert.equal(state.content, 'Edited post'); assert.equal(state.comments.length, 0);
    assert.equal(await page.getByRole('link', { name: 'Ví của tôi' }).count(), 0);
  });
} finally { await browser.close(); }
