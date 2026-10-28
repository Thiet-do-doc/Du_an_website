import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { authenticatedFetch } from '../auth/api';
type Post = { id: string; authorId: string; authorName: string; content: string; createdAtUtc: string; isMine: boolean };
type Feed = { items: Post[]; hasMore: boolean };
export default function FeedPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [content, setContent] = useState('');
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true); setError('');
    authenticatedFetch<Feed>(`/api/v1/posts?page=${page}`).then(result => {
      if (!active) return;
      setPosts(old => page === 1 ? result.items : Array.from(new Map([...old, ...result.items].map(p => [p.id, p])).values()));
      setHasMore(result.hasMore);
    }).catch(() => { if (active) setError('Chưa tải được bảng tin. Bạn có thể thử lại sau ít phút.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, reload]);
  const refreshFeed = useCallback(() => { setPage(1); setReload(v => v + 1); }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!content.trim() || busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await authenticatedFetch<Post>('/api/v1/posts', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content }) });
      setContent(''); setNotice('Đã chia sẻ bài viết của bạn.'); refreshFeed();
    } catch (e) { setError(e instanceof Error ? e.message : 'Chưa đăng được bài. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  async function remove(id: string) {
    if (!window.confirm('Xóa bài viết này của bạn?')) return;
    setBusy(true); setError('');
    try { await authenticatedFetch(`/api/v1/posts/${id}`, { method: 'DELETE' }); setPosts(old => old.filter(p => p.id !== id)); setNotice('Đã xóa bài viết.'); }
    catch { setError('Chưa xóa được bài viết. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  return <div className="feed-layout"><section>
    <div className="feed-hero"><p className="ml-kicker">GÓC NHỎ CỦA CHÚNG MÌNH</p><h1>Hôm nay bạn thế nào?</h1><p>Một câu chuyện nhỏ cũng có thể là khởi đầu của một kết nối thật đẹp.</p></div>
    <form className="ml-card feed-composer" onSubmit={submit}><h2>Chia sẻ một chút về ngày của bạn</h2><label className="visually-hidden" htmlFor="post-content">Nội dung bài viết</label><textarea id="post-content" maxLength={2000} required value={content} onChange={e => setContent(e.target.value)} placeholder="Một niềm vui, một điều bạn yêu thích…" /><div className="composer-bottom"><small>Bài viết hiển thị với các thành viên mylove.<br />{content.length}/2000 ký tự</small><button className="btn btn-dark" disabled={busy || !content.trim()}>{busy ? 'Đang xử lý…' : 'Chia sẻ ↗'}</button></div></form>
    {error && <div className="feed-error" role="alert">{error} <button className="btn btn-sm" onClick={refreshFeed} disabled={loading}>Thử lại</button></div>}
    {notice && <p className="text-success small" role="status">{notice}</p>}
    <div className="feed-title"><h2>Bảng tin</h2><span>Mới nhất</span></div>
    {posts.map(post => <article className="ml-card post-card" key={post.id}><div className="post-top"><span className="post-avatar" aria-hidden="true">{post.authorName.slice(0, 1).toUpperCase()}</span><div><h3>{post.authorName}{post.isMine ? ' · Bạn' : ''}</h3><time dateTime={post.createdAtUtc}>{new Date(post.createdAtUtc).toLocaleString('vi-VN', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })}</time></div>{post.isMine && <button className="post-delete" disabled={busy} onClick={() => void remove(post.id)} aria-label={`Xóa bài viết của ${post.authorName}`}>Xóa</button>}</div><p className="post-body">{post.content}</p><div className="post-footer"><span>Một chia sẻ trên mylove</span>{!post.isMine && <a href={`/safety?user=${encodeURIComponent(post.authorId)}`}>An toàn & chặn</a>}</div></article>)}
    {loading && <p role="status">Đang tải những câu chuyện mới…</p>}
    {!loading && !error && !posts.length && <div className="ml-card feed-empty"><span aria-hidden="true">♡</span><h2>Một trang mới, một câu chuyện mới.</h2><p>Chưa có bài viết nào để hiển thị. Chia sẻ điều đầu tiên của bạn nhé!</p></div>}
    {hasMore && <button className="btn btn-outline-secondary feed-load" disabled={loading} onClick={() => setPage(p => p + 1)}>Xem thêm bài viết</button>}
  </section><aside className="feed-sidebar"><div className="ml-card"><div className="side-art" aria-hidden="true">✦</div><h2>Để mọi người hiểu bạn hơn</h2><p>Một tấm ảnh, đôi dòng giới thiệu và những điều bạn yêu thích.</p><a className="text-link" href="/profile">Hoàn thiện hồ sơ ↗</a></div><div className="ml-card"><p className="ml-kicker">CÓ THỂ BẠN SẼ GẶP</p><h2>Một người cùng tần số</h2><p>Ghé Khám phá và bắt đầu bằng một lời thích.</p><a className="text-link" href="/discover">Khám phá ngay ↗</a></div><div className="ml-card"><span className="soon-pill">Sắp ra mắt</span><h2>Trao một chút yêu thương</h2><p>Góc quà tặng và ví mylove đang được chuẩn bị.</p><a className="text-link" href="/gifts">Ghé góc quà tặng ↗</a></div></aside></div>;
}
