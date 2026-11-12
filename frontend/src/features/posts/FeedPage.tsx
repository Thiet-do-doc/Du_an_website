import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ApiError, authenticatedFetch } from '../auth/api';
import { photoTypes, prepareUpload } from '../../shared/uploadImage';
import { PostCard, type Post } from './PostCard';
import { NavIcon } from '../../app/NavIcon';
import { AccountAvatar } from '../profiles/AccountAvatar';
import './posts.css';
type Feed = { items: Post[]; hasMore: boolean };
export default function FeedPage({ avatar }: { avatar?: string }) {
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
  return <section className="feed-page">
    <header className="workspace-intro"><h1>{sharedId ? 'Bài viết' : 'Bảng tin'}</h1>
      {!sharedId && <p>Những cập nhật mới từ cộng đồng mylove.</p>}</header>
    <div className={sharedId ? 'feed-layout shared-feed' : 'feed-layout'}><div className="feed-stream">
    {sharedId ? <a className="text-link" href="/">← Về bảng tin</a> :
      <form className="ml-card feed-composer" onSubmit={submit}>
        <div className="feed-composer-body">
          <AccountAvatar source={avatar} />
          <label className="visually-hidden" htmlFor="post-content">Nội dung bài viết</label>
          <textarea id="post-content" maxLength={2000} value={content} disabled={busy}
            onChange={e => setContent(e.target.value)} placeholder="Bạn muốn chia sẻ điều gì?" />
        </div>
        <div className="composer-tools">
          <label className="feed-tool feed-photo-tool"><NavIcon name="camera" />Ảnh<input type="file" className="visually-hidden" aria-label="Thêm ảnh" accept={photoTypes} disabled={busy}
            onChange={e => { const file = e.target.files?.[0]; if (file) setPhoto(file); e.target.value = ''; }} /></label>
          <label className="feed-tool"><NavIcon name="smile" /><select aria-label="Cảm xúc" value={feeling} disabled={busy} onChange={e => setFeeling(e.target.value)}>
            <option value="">Cảm xúc</option>
            {['Vui vẻ','Hạnh phúc','Biết ơn','Hào hứng','Buồn','Bình yên'].map(f => <option key={f}>{f}</option>)}
          </select></label>
        </div>
        {photo && <div className="composer-preview">{preview && <img src={preview} alt="Ảnh đã chọn" />}
          <span>{photo.name}</span><button type="button" disabled={busy} onClick={() => setPhoto(null)}>Bỏ ảnh</button></div>}
        <div className="composer-bottom"><small>Bài viết hiển thị với các thành viên mylove.<br />{content.length}/2000 ký tự</small>
          <button className="btn btn-dark" disabled={busy || !canPost}>{busy ? 'Đang đăng…' : 'Đăng bài'}</button></div>
      </form>}
    {error && <div className="feed-error" role="alert">{error} <button className="btn btn-sm" onClick={refreshFeed} disabled={loading}>Thử lại</button></div>}
    {notice && <p className="text-success small" role="status">{notice}</p>}
    {posts.map(post => <PostCard key={post.id} post={post} deleting={busy}
      onChange={next => setPosts(list => list.map(p => p.id === next.id ? next : p))}
      onBlockAuthor={(authorId, authorName) => {
        setPosts(old => old.filter(p => p.authorId !== authorId));
        setNotice(`Đã chặn ${authorName}. Bạn có thể bỏ chặn trong mục An toàn.`);
      }}
      onDelete={() => void remove(post.id)} />)}
    {loading && <p role="status">Đang tải những câu chuyện mới…</p>}
    {!loading && !error && !posts.length && <div className="ml-card feed-empty"><h2>Chưa có bài viết</h2><p>Bạn có thể chia sẻ bài viết đầu tiên từ ô đăng bài.</p></div>}
    {hasMore && <button className="btn btn-outline-secondary feed-load" disabled={loading} onClick={() => setPage(p => p + 1)}>Xem thêm bài viết</button>}
    </div>{!sharedId && <aside className="feed-sidebar">
      <section className="ml-card"><h2>Hồ sơ của bạn</h2><p>Chỉnh sửa lời giới thiệu, ảnh và sở thích.</p><a className="text-link" href="/profile">Chỉnh sửa hồ sơ →</a></section>
      <section className="ml-card"><h2>Khám phá thành viên</h2><p>Xem hồ sơ theo khu vực và sở thích.</p><a className="text-link" href="/discover">Mở Khám phá →</a></section>
    </aside>}</div>
  </section>;
}
