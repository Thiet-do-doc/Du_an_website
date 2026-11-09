import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ApiError, authenticatedFetch } from '../auth/api';
import { photoTypes, prepareUpload } from '../../shared/uploadImage';
import { PostCard, type Post } from './PostCard';
import './posts.css';
type Feed = { items: Post[]; hasMore: boolean };
export default function FeedPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [content, setContent] = useState('');
  const [feeling, setFeeling] = useState('');
  const [photo, setPhoto] = useState<File | null>(null);
  const [preview, setPreview] = useState('');
  const sharedId = new URLSearchParams(location.search).get('post');
  const canPost = Boolean(content.trim() || feeling || photo);
  useEffect(() => {
    if (!photo) { setPreview(''); return; }
    const url = URL.createObjectURL(photo);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [photo]);
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
    const request = sharedId
      ? authenticatedFetch<Post>('/api/v1/posts/' + encodeURIComponent(sharedId)).then(post => ({ items: [post], hasMore: false }))
      : authenticatedFetch<Feed>(`/api/v1/posts?page=${page}`);
    request.then(result => {
      if (!active) return;
      setPosts(old => page === 1 ? result.items : Array.from(new Map([...old, ...result.items].map(p => [p.id, p])).values()));
      setHasMore(result.hasMore);
    }).catch(e => { if (active) setError(sharedId && e instanceof ApiError && e.status === 404
      ? 'Bài viết đã bị xóa hoặc bạn không có quyền xem.' : 'Chưa tải được bảng tin. Bạn có thể thử lại sau ít phút.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, reload, sharedId]);
  const refreshFeed = useCallback(() => { setPage(1); setReload(v => v + 1); }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!canPost || busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const body = new FormData();
      body.set('content', content.trim());
      if (feeling) body.set('feeling', feeling);
      if (photo) body.set('file', await prepareUpload(photo));
      await authenticatedFetch<Post>('/api/v1/posts', { method: 'POST', body });
      setContent(''); setFeeling(''); setPhoto(null); setNotice('Đã đăng bài viết của bạn.'); refreshFeed();
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
    {sharedId ? <a className="text-link" href="/">← Về bảng tin</a> :
      <form className="ml-card feed-composer" onSubmit={submit}>
        <h2>Chia sẻ một chút về ngày của bạn</h2>
        <label className="visually-hidden" htmlFor="post-content">Nội dung bài viết</label>
        <textarea id="post-content" maxLength={2000} value={content} disabled={busy}
          onChange={e => setContent(e.target.value)} placeholder="Một niềm vui, một điều bạn yêu thích…" />
        <div className="composer-tools">
          <label>Thêm ảnh<input type="file" accept={photoTypes} disabled={busy}
            onChange={e => { const file = e.target.files?.[0]; if (file) setPhoto(file); e.target.value = ''; }} /></label>
          <label>Cảm xúc<select value={feeling} disabled={busy} onChange={e => setFeeling(e.target.value)}>
            <option value="">Chọn cảm xúc</option>
            {['Vui vẻ','Hạnh phúc','Biết ơn','Hào hứng','Buồn','Bình yên'].map(f => <option key={f}>{f}</option>)}
          </select></label>
        </div>
        {photo && <div className="composer-preview">{preview && <img src={preview} alt="Ảnh đã chọn" />}
          <span>{photo.name}</span><button type="button" disabled={busy} onClick={() => setPhoto(null)}>Bỏ ảnh</button></div>}
        <div className="composer-bottom"><small>Bài viết hiển thị với các thành viên mylove.<br />{content.length}/2000 ký tự</small>
          <button className="btn btn-dark" disabled={busy || !canPost}>{busy ? 'Đang đăng…' : 'Đăng bài ↗'}</button></div>
      </form>}
    {error && <div className="feed-error" role="alert">{error} <button className="btn btn-sm" onClick={refreshFeed} disabled={loading}>Thử lại</button></div>}
    {notice && <p className="text-success small" role="status">{notice}</p>}
    <div className="feed-title"><h2>Bảng tin</h2><span>Mới nhất</span></div>
    {posts.map(post => <PostCard key={post.id} post={post} deleting={busy}
      onChange={next => setPosts(list => list.map(p => p.id === next.id ? next : p))}
      onBlockAuthor={(authorId, authorName) => {
        setPosts(old => old.filter(p => p.authorId !== authorId));
        setNotice(`Đã chặn ${authorName}. Bạn có thể bỏ chặn trong mục An toàn.`);
      }}
      onDelete={() => void remove(post.id)} />)}
    {loading && <p role="status">Đang tải những câu chuyện mới…</p>}
    {!loading && !error && !posts.length && <div className="ml-card feed-empty"><span aria-hidden="true">♡</span><h2>Một trang mới, một câu chuyện mới.</h2><p>Chưa có bài viết nào để hiển thị. Chia sẻ điều đầu tiên của bạn nhé!</p></div>}
    {hasMore && <button className="btn btn-outline-secondary feed-load" disabled={loading} onClick={() => setPage(p => p + 1)}>Xem thêm bài viết</button>}
  </section><aside className="feed-sidebar"><div className="ml-card"><div className="side-art" aria-hidden="true">✦</div><h2>Để mọi người hiểu bạn hơn</h2><p>Một tấm ảnh, đôi dòng giới thiệu và những điều bạn yêu thích.</p><a className="text-link" href="/profile">Hoàn thiện hồ sơ ↗</a></div><div className="ml-card"><p className="ml-kicker">CÓ THỂ BẠN SẼ GẶP</p><h2>Một người cùng tần số</h2><p>Ghé Khám phá và bắt đầu bằng một lời thích.</p><a className="text-link" href="/discover">Khám phá ngay ↗</a></div><div className="ml-card"><span className="soon-pill">Quà ảo miễn phí</span><h2>Trao một chút yêu thương</h2><p>Mở hội thoại và chọn nút quà tặng bên cạnh ô nhập tin nhắn.</p><a className="text-link" href="/chat">Tặng quà trong trò chuyện ↗</a></div></aside></div>;
}
