import { authenticatedFetch } from '../auth/api';
import { FormEvent, useState } from 'react';

type ReportType = 'User' | 'Message' | 'Post';

const request = <T,>(path: string, init: RequestInit = {}) => authenticatedFetch<T>(path, {
  ...init, headers: { 'Content-Type': 'application/json', ...init.headers },
});

export default function SafetyAdminPanel() {
  const [targetType, setTargetType] = useState<ReportType>('User');
  const [targetId, setTargetId] = useState(new URLSearchParams(location.search).get('user') ?? '');
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [blockTarget, setBlockTarget] = useState(new URLSearchParams(location.search).get('user') ?? '');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);

  async function submitReport(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      await request('/api/v1/reports', { method: 'POST', body: JSON.stringify({ targetType, targetId, reason, description }) });
      setTargetId(''); setReason(''); setDescription(''); setMessage('Đã gửi báo cáo. Nhóm kiểm duyệt sẽ xem xét.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không gửi được báo cáo.'); }
    finally { setBusy(false); }
  }

  async function changeBlock(event: FormEvent | null, action: 'block' | 'unblock') {
    event?.preventDefault(); setBusy(true); setMessage('');
    try {
      await request(`/api/v1/blocks/${encodeURIComponent(blockTarget)}`, { method: action === 'block' ? 'POST' : 'DELETE' });
      setBlockTarget(''); setMessage(action === 'block' ? 'Đã chặn người dùng.' : 'Đã bỏ chặn người dùng.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không cập nhật được trạng thái chặn.'); }
    finally { setBusy(false); }
  }

  return <section className="safety-panel" aria-labelledby="safety-heading">
    <p className="eyebrow">AN TOÀN CỘNG ĐỒNG</p><h2 id="safety-heading">Chặn và báo cáo</h2>
    <p className="safety-note">Nhập ID người dùng hoặc tin nhắn cần xử lý. Không gửi nội dung chat riêng vào phần mô tả.</p>
    <div className="safety-grid">
      <form className="safety-card" onSubmit={event => void changeBlock(event, 'block')}>
        <h3>Chặn người dùng</h3><label>ID người dùng<input required value={blockTarget} onChange={e => setBlockTarget(e.target.value)} /></label>
        <div className="safety-actions"><button className="btn btn-dark" disabled={busy}>Chặn</button>
          <button type="button" className="btn btn-outline-secondary" disabled={busy || !blockTarget} onClick={() => void changeBlock(null, 'unblock')}>Bỏ chặn</button></div>
      </form>
      <form className="safety-card" onSubmit={submitReport}>
        <h3>Gửi báo cáo</h3>
        <label>Đối tượng<select value={targetType} onChange={e => setTargetType(e.target.value as ReportType)}>
          <option value="User">Người dùng</option><option value="Message">Tin nhắn</option>
        </select></label>
        <label>ID đối tượng<input required maxLength={200} value={targetId} onChange={e => setTargetId(e.target.value)} /></label>
        <label>Lý do<select required value={reason} onChange={e => setReason(e.target.value)}>
          <option value="">Chọn lý do</option><option>Quấy rối</option><option>Lừa đảo</option><option>Nội dung không phù hợp</option><option>Khác</option>
        </select></label>
        <label>Mô tả<textarea required minLength={10} maxLength={2000} rows={3} value={description} onChange={e => setDescription(e.target.value)} /></label>
        <button className="btn btn-dark" disabled={busy}>Gửi báo cáo</button>
      </form>
    </div>
    <p aria-live="polite" className="safety-message">{message}</p>
  </section>;
}
