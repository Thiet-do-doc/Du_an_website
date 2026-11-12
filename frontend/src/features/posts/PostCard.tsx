import { useState } from 'react';
import { authenticatedFetch } from '../auth/api';
import { blockUser } from '../safety-admin/api/blocks';
import { CommentThread } from './CommentThread';
import { PrivatePhoto } from '../profiles/PrivatePhoto';
import { NavIcon } from '../../app/NavIcon';

export type Post = {
  id: string; authorId: string; authorName: string; content: string; createdAtUtc: string; isMine: boolean;
  hasPhoto: boolean; feeling: string | null; likeCount: number; isLiked: boolean; commentCount: number;
  canOpenAuthorProfile: boolean; authorAvatarId: string | null;
};
const date = (value: string) => new Date(value).toLocaleString('vi-VN', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

export function PostCard({ post, onChange, onDelete, onBlockAuthor, deleting }: {
  post: Post; onChange: (post: Post) => void; onDelete: () => void; onBlockAuthor?: (authorId: string, authorName: string) => void; deleting: boolean;
}) {
  const [open, setOpen] = useState(new URLSearchParams(location.search).get('comments') === '1');
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
  async function toggleLike() {
    await authenticatedFetch(path + '/like', { method: post.isLiked ? 'DELETE' : 'PUT' });
    onChange(await authenticatedFetch<Post>(path));
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
  async function blockAuthor() {
    if (!window.confirm(`Chặn ${post.authorName}? Bài viết, bình luận và hồ sơ của người này sẽ bị ẩn khỏi bạn.`)) return;
    await blockUser(post.authorId);
    window.dispatchEvent(new Event('notifications-read'));
    onBlockAuthor?.(post.authorId, post.authorName);
  }
  return <article className="ml-card post-card">
    <header className="post-top">{post.canOpenAuthorProfile
      ? <a href={profileUrl} className="post-avatar" aria-label={'Xem hồ sơ của ' + post.authorName}>{avatar}</a>
      : <span className="post-avatar" aria-hidden="true">{avatar}</span>}
      <div><h3>{post.canOpenAuthorProfile ? <a className="post-author" href={profileUrl}>{post.authorName}</a> : post.authorName}{post.isMine && ' · Bạn'}</h3>
        <time dateTime={post.createdAtUtc}>{date(post.createdAtUtc)}</time></div>
      {post.isMine && <button type="button" className="post-delete" disabled={deleting} onClick={onDelete}>Xóa bài</button>}
    </header>
    {post.feeling && <p className="post-feeling"><NavIcon name="smile" />Đang cảm thấy {post.feeling.toLocaleLowerCase('vi')}</p>}
    {post.content && <p className="post-body">{post.content}</p>}
    {post.hasPhoto && <PrivatePhoto className="post-image" src={path + '/photo'} alt={'Ảnh trong bài của ' + post.authorName} />}
    <div className="post-actions">
      <button type="button" aria-pressed={post.isLiked} disabled={busy} onClick={() => void run(toggleLike)}>
        <NavIcon name="heart" />{post.isLiked ? 'Đã thích' : 'Thích'} · {post.likeCount}</button>
      <button type="button" aria-expanded={open} disabled={busy} onClick={() => {
        setOpen(!open);
      }}><NavIcon name="chat" />Bình luận · {post.commentCount}</button>
      <button type="button" onClick={() => void share()}><NavIcon name="share" />Chia sẻ</button>
    </div>
    {shareNotice && <p role="status">{shareNotice}</p>}
    {copyLink && <label>Liên kết bài viết<input readOnly value={link} onFocus={e => e.target.select()} /></label>}
    {open && <CommentThread postId={post.id} onChange={async () => { onChange(await authenticatedFetch<Post>(path)); }} />}
    {error && <p role="alert" className="text-danger">{error}</p>}
    {!post.isMine && <div className="post-safety">
      <a className="text-link" href={'/safety?user=' + encodeURIComponent(post.authorId)}>Báo cáo người dùng</a>
      <button type="button" className="post-block" disabled={busy} onClick={() => void run(blockAuthor)}>Chặn người dùng</button>
    </div>}
  </article>;
}
