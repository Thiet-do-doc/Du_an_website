import { useCallback, useEffect, useRef, useState } from 'react';
import { authenticatedFetch } from '../../auth/api';
import { getNotifications, markRead } from '../api/chatApi';
import type { ChatNotification } from '../types/chat';
import './notifications.css';

const labels: Record<ChatNotification['type'], string> = {
  match: 'Bạn có một kết nối mới. Bắt đầu trò chuyện nhé!', newMessage: 'Bạn có tin nhắn mới.',
  profileLike: 'Có người đã thích bạn.', friendPost: 'Một người bạn vừa đăng bài.',
  postLike: 'Có người thích bài viết của bạn.', comment: 'Có bình luận mới trên bài viết của bạn.', reply: 'Có người trả lời bình luận của bạn.',
};
function href(item: ChatNotification) {
  if (item.conversationId) return '/chat?conversation=' + item.conversationId;
  if (item.type === 'profileLike') return '/profile?user=' + item.targetId;
  return '/?post=' + item.targetId + (['comment', 'reply'].includes(item.type) ? '&comments=1' : '');
}
export function NotificationBadge() {
  const [count, setCount] = useState(0);
  useEffect(() => {
    let active = true;
    const update = () => {
      void authenticatedFetch<{ count: number }>('/api/v1/notifications/unread-count')
        .then(data => { if (active) setCount(data.count); }).catch(() => {});
    };
    update();
    const timer = window.setInterval(update, 15000);
    window.addEventListener('notifications-read', update);
    window.addEventListener('focus', update);
    return () => { active = false; clearInterval(timer); window.removeEventListener('notifications-read', update); window.removeEventListener('focus', update); };
  }, []);
  return count > 0 ? <span className="notification-badge" aria-label={count + ' thông báo chưa đọc'}>{count > 99 ? '99+' : count}</span> : null;
}
export default function NotificationsPage() {
  const [items, setItems] = useState<ChatNotification[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [unreadOnly, setUnreadOnly] = useState(false);
  const alive = useRef(true);
  const loading = useRef(false);
  const load = useCallback(async (older?: string) => {
    if (loading.current) return;
    loading.current = true; setBusy(true); setError('');
    try {
      const page = await getNotifications('', { cursor: older });
      if (!alive.current) return;
      setItems(old => older ? [...old, ...page.items.filter(x => !old.some(y => y.id === x.id))] : page.items);
      setCursor(page.nextCursor);
    } catch (e) { if (alive.current) setError(e instanceof Error ? e.message : 'Chưa tải được thông báo.'); }
    finally { loading.current = false; if (alive.current) setBusy(false); }
  }, []);
  useEffect(() => {
    alive.current = true; void load();
    return () => { alive.current = false; };
  }, [load]);
  async function read(item: ChatNotification) {
    try {
      const saved = await markRead('', item.id);
      setItems(old => old.map(x => x.id === saved.id ? saved : x));
      window.dispatchEvent(new Event('notifications-read'));
    } catch (e) { setError(e instanceof Error ? e.message : 'Chưa đánh dấu được thông báo.'); }
  }
  const shown = items.filter(x => !unreadOnly || !x.readAt);
  return <section className="notification-page feature-page">
    <header><p className="ml-kicker">NHỊP KẾT NỐI</p><h1>Thông báo</h1><p className="page-lead">Lượt thích, bài viết mới và những cuộc trò chuyện đang chờ bạn.</p></header>
    <div className="notification-filters"><button aria-pressed={!unreadOnly} onClick={() => setUnreadOnly(false)}>Tất cả</button>
      <button aria-pressed={unreadOnly} onClick={() => setUnreadOnly(true)}>Chưa đọc</button>
      <button disabled={busy} onClick={() => void load()}>Làm mới</button></div>
    {error && <p role="alert">{error}</p>}
    {busy && <p role="status">Đang tải thông báo…</p>}
    {!busy && !error && !shown.length && <div className="ml-card"><p>{unreadOnly ? 'Không có thông báo chưa đọc trong phần đã tải.' : 'Bạn chưa có thông báo. Những kết nối mới sẽ xuất hiện ở đây.'}</p></div>}
    <ul className="notification-list">{shown.map(item => <li key={item.id} className={item.readAt ? '' : 'unread'}>
      <span className="notification-symbol" aria-hidden="true">{['match', 'profileLike', 'postLike'].includes(item.type) ? '♡' : ['comment', 'reply', 'newMessage'].includes(item.type) ? '☏' : '✦'}</span>
      <div><a href={href(item)} onClick={() => { if (!item.readAt) void read(item); }}>{item.text || labels[item.type]}</a>
        <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString('vi-VN')}</time>
        {!item.readAt && <button onClick={() => void read(item)}>Đánh dấu đã đọc</button>}</div>
      {!item.readAt && <span className="notification-dot" aria-label="Chưa đọc" />}
    </li>)}</ul>
    {cursor && <button className="btn btn-outline-dark" disabled={busy} onClick={() => void load(cursor)}>Xem thông báo trước</button>}
  </section>;
}
