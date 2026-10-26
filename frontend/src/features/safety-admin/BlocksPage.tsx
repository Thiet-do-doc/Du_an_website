import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ProtectedRoute, useAuth } from '../auth/AuthProvider';
import { blockUser, getBlockedUsers } from './api/blocks';
import type { BlockedUser } from './types/block';
import BlockButton from './BlockButton';
function BlocksContent() {
  const { session } = useAuth();
  const [items, setItems] = useState<BlockedUser[]>([]);
  const [target, setTarget] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try { setItems(await getBlockedUsers()); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không tải được danh sách chặn.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError(''); setMessage('');
    try { await blockUser(target.trim()); setTarget(''); setMessage('Đã chặn người dùng.'); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không chặn được người dùng.'); }
    finally { setBusy(false); }
  }
  return <section><h1>Danh sách chặn</h1>
    <p>Chặn ngăn tương tác theo cả hai chiều. Bỏ chặn không tự khôi phục ghép đôi đã hủy.</p>
    <form onSubmit={e => void submit(e)}><label>ID người dùng cần chặn
      <input className="form-control" required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" value={target} onChange={e => setTarget(e.target.value)} /></label>
      <button className="btn btn-dark ms-2" disabled={busy || target.trim().toLowerCase() === session?.user.id.toLowerCase()}>Chặn</button></form>
    {error && <p role="alert">{error}</p>}{message && <p role="status">{message}</p>}
    <button className="btn btn-link" disabled={loading} onClick={() => void load()}>Tải lại</button>
    {loading ? <p role="status">Đang tải…</p> : !error && items.length === 0 ? <p>Bạn chưa chặn ai.</p> :
      <ul>{items.map(item => <li key={item.userId}><code>{item.userId}</code> · {new Date(item.blockedAt).toLocaleString('vi-VN')} · <BlockButton userId={item.userId} blocked onChanged={load} /></li>)}</ul>}
  </section>;
}
export default function BlocksPage() { return <ProtectedRoute><BlocksContent /></ProtectedRoute>; }
