import { useState, type FormEvent } from 'react';
import { authenticatedFetch } from '../auth/api';
import { PrivatePhoto } from '../profiles/PrivatePhoto';

export type Post = {
  id: string; authorId: string; authorName: string; content: string; createdAtUtc: string; isMine: boolean;
  hasPhoto: boolean; feeling: string | null; likeCount: number; isLiked: boolean; commentCount: number;
  canOpenAuthorProfile: boolean; authorAvatarId: string | null;
};
type Comment = { id: string; authorName: string; content: string; createdAtUtc: string };
type CommentsPage = { items: Comment[]; hasMore: boolean };
const date = (value: string) => new Date(value).toLocaleString('vi-VN', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

export function PostCard({ post, onChange, onDelete, deleting }: {
  post: Post; onChange: (post: Post) => void; onDelete: () => void; deleting: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [comments, setComments] = useState<Comment[]>([]);
  const [page, setPage] = useState(0);
  const [more, setMore] = useState(false);
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [shareNotice, setShareNotice] = useState('');
  const [copyLink, setCopyLink] = useState(false);
  const path = '/api/v1/posts/' + post.id;
  const profileUrl = post.isMine ? '/profile' : '/profile?user=' + encodeURIComponent(post.authorId);
  const avatar = post.authorAvatarId
    ? <PrivatePhoto src={'/api/v1/photos/' + post.authorAvatarId + '/content'} alt="" />
    : post.authorName.slice(0, 1).toUpperCase();
  const link = new URL('/?post=' + post.id, location.origin).href;
  async function run(action: () => Promise<void>) {
    setBusy(true); setError('');
    try { await action(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Chưa thực hiện được. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  async function loadPage(next: number) {
    const result = await authenticatedFetch<CommentsPage>(path + '/comments?page=' + next);
    setComments(old => next === 1 ? result.items : Array.from(new Map([...old, ...result.items].map(c => [c.id, c])).values()));
    setPage(next); setMore(result.hasMore);
  }
  async function toggleLike() {
    await authenticatedFetch(path + '/like', { method: post.isLiked ? 'DELETE' : 'PUT' });
    onChange(await authenticatedFetch<Post>(path));
  }
  async function submitComment(event: FormEvent) {
    event.preventDefault();
    if (!draft.trim() || busy) return;
    await run(async () => {
      await authenticatedFetch(path + '/comments', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: draft.trim() }) });
      setDraft('');
      await loadPage(1);
      onChange(await authenticatedFetch<Post>(path));
    });
  }
  async function share() {
    setShareNotice(''); setCopyLink(false);
    if (navigator.share) {
      try { await navigator.share({ title: 'Bài viết trên mylove', url: link }); return; }
      catch (e) { if (e instanceof Error && e.name === 'AbortError') return; }
    }
    try { await navigator.clipboard.writeText(link); setShareNotice('Đã sao chép liên kết bài viết.'); }
    catch { setCopyLink(true); }
  }
  return <article className="ml-card post-card">
    <header className="post-top">{post.canOpenAuthorProfile
      ? <a href={profileUrl} className="post-avatar" aria-label={'Xem hồ sơ của ' + post.authorName}>{avatar}</a>
      : <span className="post-avatar" aria-hidden="true">{avatar}</span>}
      <div><h3>{post.canOpenAuthorProfile ? <a className="post-author" href={profileUrl}>{post.authorName}</a> : post.authorName}{post.isMine && ' · Bạn'}</h3>
        <time dateTime={post.createdAtUtc}>{date(post.createdAtUtc)}</time></div>
      {post.isMine && <button type="button" className="post-delete" disabled={deleting} onClick={onDelete}>Xóa bài</button>}
    </header>
    {post.feeling && <p className="post-feeling">Đang cảm thấy {post.feeling.toLocaleLowerCase('vi')}</p>}
    {post.content && <p className="post-body">{post.content}</p>}
    {post.hasPhoto && <PrivatePhoto className="post-image" src={path + '/photo'} alt={'Ảnh trong bài của ' + post.authorName} />}
    <div className="post-actions">
      <button type="button" aria-pressed={post.isLiked} disabled={busy} onClick={() => void run(toggleLike)}>
        {post.isLiked ? '♥ Đã thích' : '♡ Thích'} · {post.likeCount}</button>
      <button type="button" aria-expanded={open} disabled={busy} onClick={() => {
        setOpen(!open); if (!open) void run(() => loadPage(1));
      }}>Bình luận · {post.commentCount}</button>
      <button type="button" onClick={() => void share()}>Chia sẻ ↗</button>
    </div>
    {shareNotice && <p role="status">{shareNotice}</p>}
    {copyLink && <label>Liên kết bài viết<input readOnly value={link} onFocus={e => e.target.select()} /></label>}
    {open && <section className="post-comments" aria-label="Bình luận">
      <form onSubmit={submitComment}><label>Viết bình luận<input maxLength={1000} value={draft} onChange={e => setDraft(e.target.value)} /></label>
        <button className="btn btn-dark" disabled={busy || !draft.trim()}>Gửi</button></form>
      {busy && <small role="status">Đang xử lý…</small>}
      {!busy && !error && !comments.length && <p>Chưa có bình luận.</p>}
      {comments.map(c => <div key={c.id}><strong>{c.authorName}</strong> <time dateTime={c.createdAtUtc}>{date(c.createdAtUtc)}</time><p>{c.content}</p></div>)}
      {more && <button type="button" disabled={busy} onClick={() => void run(() => loadPage(page + 1))}>Xem bình luận trước</button>}
    </section>}
    {error && <p role="alert" className="text-danger">{error}</p>}
    {!post.isMine && <a className="text-link" href={'/safety?user=' + post.authorId}>An toàn &amp; chặn</a>}
  </article>;
}
