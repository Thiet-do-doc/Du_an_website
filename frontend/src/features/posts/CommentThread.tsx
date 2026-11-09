import { useEffect, useRef, useState, type FormEvent } from 'react';
import { authenticatedFetch } from '../auth/api';
import { useAuth } from '../auth/AuthProvider';
import { getPublicProfile } from '../profiles/api';
import { ReactionPicker, type Reaction } from './ReactionPicker';

type Comment = { id: string; authorId: string; authorName: string; content: string; createdAtUtc: string;
  parentId: string | null; replyCount: number; myReaction: Reaction | null; reactions: Record<Reaction, number> };
const date = (value: string) => new Date(value).toLocaleString('vi-VN', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

export function CommentThread({ postId, parentId, parentName, onChange, onAdded, depth = 0 }: {
  postId: string; parentId?: string; parentName?: string; onChange: () => Promise<void>; onAdded?: () => void; depth?: number;
}) {
  const [items, setItems] = useState<Comment[]>([]);
  const [page, setPage] = useState(1);
  const [more, setMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [version, setVersion] = useState(0);
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const sending = useRef(false);
  const path = '/api/v1/posts/' + postId + '/comments';
  useEffect(() => {
    let active = true; setLoading(true); setError('');
    const query = new URLSearchParams({ page: String(page) });
    if (parentId) query.set('parentId', parentId);
    authenticatedFetch<{ items: Comment[]; hasMore: boolean }>(path + '?' + query).then(result => {
      if (active) {
        setItems(old => page === 1 ? result.items : Array.from(new Map([...old, ...result.items].map(c => [c.id, c])).values()));
        setMore(result.hasMore);
      }
    }).catch(e => { if (active) setError(e instanceof Error ? e.message : 'Chưa tải được bình luận.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [path, parentId, page, version]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!draft.trim() || sending.current) return;
    sending.current = true; setBusy(true); setError('');
    try {
      await authenticatedFetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: draft.trim(), parentId }) });
      setDraft(''); setPage(1); setVersion(x => x + 1); onAdded?.(); await onChange();
    } catch (e) { setError(e instanceof Error ? e.message : 'Chưa gửi được bình luận.'); }
    finally { sending.current = false; setBusy(false); }
  }
  return <section className={'comment-thread' + (parentId ? ' comment-replies' : '')} aria-label={parentName ? 'Trả lời ' + parentName : 'Bình luận'}>
    <form onSubmit={submit}><label>{parentName ? 'Trả lời ' + parentName : 'Viết bình luận'}
      <input maxLength={1000} required value={draft} disabled={busy} onChange={e => setDraft(e.target.value)} placeholder={parentName ? 'Viết câu trả lời…' : 'Chia sẻ suy nghĩ của bạn…'} /></label>
      <button className="btn btn-dark" disabled={busy || !draft.trim()}>{busy ? 'Đang gửi…' : 'Gửi'}</button></form>
    {error && <p role="alert">{error} <button onClick={() => setVersion(x => x + 1)}>Tải lại</button></p>}
    {loading && <p role="status">Đang tải bình luận…</p>}
    {!loading && !error && !items.length && <p className="comment-empty">{parentId ? 'Hãy là người đầu tiên trả lời.' : 'Chưa có bình luận.'}</p>}
    {items.map(item => <CommentItem key={item.id} item={item} path={path} depth={depth} postId={postId} onChange={onChange} />)}
    {more && <button disabled={loading} onClick={() => setPage(x => x + 1)}>Xem thêm {parentId ? 'câu trả lời' : 'bình luận'}</button>}
  </section>;
}
function CommentItem({ item, path, postId, onChange, depth }: {
  item: Comment; path: string; postId: string; onChange: () => Promise<void>; depth: number;
}) {
  const { session } = useAuth();
  const [comment, setComment] = useState(item);
  const [replyOpen, setReplyOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const inFlight = useRef(false);
  useEffect(() => { setComment(item); }, [item]);
  async function react(kind: Reaction) {
    if (inFlight.current) return;
    inFlight.current = true; setBusy(true); setError('');
    const remove = comment.myReaction === kind;
    try {
      await authenticatedFetch(path + '/' + item.id + '/reaction', { method: remove ? 'DELETE' : 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: remove ? undefined : JSON.stringify({ kind }) });
      setComment(old => {
        const counts = { ...old.reactions };
        if (old.myReaction) counts[old.myReaction] = Math.max(0, counts[old.myReaction] - 1);
        if (!remove) counts[kind]++;
        return { ...old, reactions: counts, myReaction: remove ? null : kind };
      });
    } catch (e) { setError(e instanceof Error ? e.message : 'Chưa lưu được cảm xúc.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  async function message() {
    setBusy(true); setError('');
    try {
      const profile = await getPublicProfile(item.authorId);
      if (profile.connection.state === 'matched' && profile.connection.conversationId) {
        history.pushState(null, '', '/chat?conversation=' + profile.connection.conversationId);
        window.dispatchEvent(new PopStateEvent('popstate'));
      } else setError('Hai bạn cần ghép đôi trước khi nhắn tin. Bạn có thể mở hồ sơ để kết nối.');
    } catch { setError('Hiện chưa thể nhắn tin với người này.'); }
    finally { setBusy(false); }
  }
  return <article className="comment-item">
    <div className="comment-content"><strong>{comment.authorName}</strong> <time dateTime={comment.createdAtUtc}>{date(comment.createdAtUtc)}</time><p>{comment.content}</p></div>
    <div className="comment-actions" aria-label="Tương tác bình luận">
      <ReactionPicker value={comment.myReaction} counts={comment.reactions} disabled={busy} onSelect={kind => void react(kind)} />
      <button aria-expanded={replyOpen} onClick={() => setReplyOpen(!replyOpen)}>Trả lời{comment.replyCount > 0 && ' · ' + comment.replyCount}</button>
      {item.authorId !== session?.user.id && <><button disabled={busy} onClick={() => void message()}>Nhắn tin</button><a href={'/profile?user=' + item.authorId}>Hồ sơ</a></>}
    </div>
    {error && <p role="alert">{error}</p>}
    {replyOpen && <div style={{ marginLeft: depth < 2 ? 16 : 0 }}><CommentThread postId={postId} parentId={comment.id} parentName={comment.authorName} depth={depth + 1} onChange={onChange} onAdded={() => setComment(old => ({ ...old, replyCount: old.replyCount + 1 }))} /></div>}
  </article>;
}
