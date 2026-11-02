import { useState, type FormEvent } from 'react';
import { ApiError } from '../../auth/api';
import { createReport, type ReportTargetType } from '../api/reports';

type Props = { targetType: ReportTargetType; targetId: string; allowTargetSelection?: boolean; onSubmitted?: () => void };
/** Use key={targetId} when switching objects. Contextual forms keep the target fixed. */
export default function ReportForm({ targetType: initialType, targetId: initialId, allowTargetSelection = false, onSubmitted }: Props) {
  const [targetType, setTargetType] = useState(initialType);
  const [targetId, setTargetId] = useState(initialId);
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [sent, setSent] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true); setError(''); setSent(false);
    try {
      await createReport({ targetType, targetId, reason, description });
      setSent(true); setReason(''); setDescription(''); onSubmitted?.();
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? 'Đối tượng không tồn tại hoặc bạn không có quyền báo cáo.'
        : e instanceof Error ? e.message : 'Không gửi được báo cáo.');
    } finally { setBusy(false); }
  }
  return <form className="safety-card" onSubmit={event => void submit(event)} aria-busy={busy}>
    <h3>Gửi báo cáo</h3>
    <fieldset disabled={busy}>
      {allowTargetSelection ? <>
        <label>Đối tượng<select value={targetType} onChange={e => { setTargetType(e.target.value as ReportTargetType); setSent(false); }}>
          <option value="User">Người dùng</option><option value="Message">Tin nhắn</option>
        </select></label>
        <label>ID đối tượng<input required maxLength={200} value={targetId} onChange={e => { setTargetId(e.target.value); setSent(false); }} /></label>
      </> : <p>Đối tượng: {targetType === 'Message' ? 'Tin nhắn' : 'Người dùng'} · <code>{targetId}</code></p>}
      <label>Lý do<select required value={reason} onChange={e => { setReason(e.target.value); setSent(false); }}>
        <option value="">Chọn lý do</option><option>Quấy rối</option><option>Lừa đảo</option><option>Nội dung không phù hợp</option><option>Khác</option>
      </select></label>
      <label>Mô tả<textarea required minLength={10} maxLength={2000} rows={3} value={description} onChange={e => { setDescription(e.target.value); setSent(false); }} /></label>
      <button className="btn btn-dark" disabled={busy || !targetId.trim() || !reason.trim() || description.trim().length < 10}>Gửi báo cáo</button>
    </fieldset>
    {busy && <p role="status">Đang gửi…</p>}
    {sent && <p role="status">Đã gửi báo cáo. Nhóm kiểm duyệt sẽ xem xét.</p>}
    {error && <p role="alert">{error}</p>}
  </form>;
}
