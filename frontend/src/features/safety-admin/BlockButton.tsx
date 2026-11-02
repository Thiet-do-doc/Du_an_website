import { useState } from 'react';
import { blockUser, unblockUser } from './api/blocks';
type Props = { userId: string; blocked: boolean; onChanged: () => void | Promise<void> };
export default function BlockButton({ userId, blocked, onChanged }: Props) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function change() {
    setBusy(true); setError('');
    try { await (blocked ? unblockUser(userId) : blockUser(userId)); await onChanged(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không cập nhật được trạng thái chặn.'); }
    finally { setBusy(false); }
  }
  return <span><button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={() => void change()}>
    {busy ? 'Đang xử lý…' : blocked ? 'Bỏ chặn' : 'Chặn'}</button>{error && <span role="alert">{error}</span>}</span>;
}
