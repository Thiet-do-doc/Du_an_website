import { FormEvent, useCallback, useEffect, useState } from 'react';

type ReportType = 'User' | 'Message' | 'Post';
type ReportStatus = 'Pending' | 'Reviewed' | 'Dismissed' | 'Actioned';
type Report = {
  id: string; reporterUserId: string; targetType: ReportType; targetId: string;
  targetUserId?: string | null; reason: string; description: string; status: ReportStatus;
  reviewedByUserId?: string | null; reviewNotes?: string | null; createdAtUtc: string;
  reviewedAtUtc?: string | null;
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init, credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string; title?: string } | null;
    throw new Error(body?.message ?? body?.title ?? `Yêu cầu thất bại (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export default function SafetyAdminPanel() {
  const [targetType, setTargetType] = useState<ReportType>('User');
  const [targetId, setTargetId] = useState('');
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [blockTarget, setBlockTarget] = useState('');
  const [reports, setReports] = useState<Report[]>([]);
  const [selected, setSelected] = useState<Report | null>(null);
  const [reviewNotes, setReviewNotes] = useState('');
  const [accountReason, setAccountReason] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);

  const loadReports = useCallback(async () => {
    try { setReports(await request<Report[]>('/api/v1/admin/reports?status=Pending')); }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Không tải được danh sách báo cáo.'); }
  }, []);
  useEffect(() => { void loadReports(); }, [loadReports]);

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

  async function updateReport(status: ReportStatus) {
    if (!selected) return;
    setBusy(true);
    try {
      await request(`/api/v1/admin/reports/${selected.id}`, { method: 'PATCH', body: JSON.stringify({ status, notes: reviewNotes }) });
      setSelected(null); setReviewNotes(''); setMessage('Đã lưu kết quả xử lý cùng nhật ký kiểm duyệt.'); await loadReports();
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không cập nhật được báo cáo.'); }
    finally { setBusy(false); }
  }

  async function setAccountActive(isActive: boolean) {
    if (!selected?.targetUserId) return;
    setBusy(true);
    try {
      await request(`/api/v1/admin/users/${encodeURIComponent(selected.targetUserId)}/status`, {
        method: 'PATCH', body: JSON.stringify({ isActive, reason: accountReason }),
      });
      setAccountReason(''); setMessage(isActive ? 'Đã mở tài khoản.' : 'Đã khóa tài khoản và cập nhật security stamp.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không cập nhật được tài khoản.'); }
    finally { setBusy(false); }
  }

  return <section className="safety-panel" aria-labelledby="safety-heading">
    <p className="eyebrow">AN TOÀN CỘNG ĐỒNG</p><h2 id="safety-heading">Chặn và báo cáo</h2>
    <p className="safety-note">Nhập ID người dùng, tin nhắn hoặc bài viết cần xử lý. Không gửi nội dung chat riêng vào phần mô tả.</p>
    <div className="safety-grid">
      <form className="safety-card" onSubmit={event => void changeBlock(event, 'block')}>
        <h3>Chặn người dùng</h3><label>ID người dùng<input required value={blockTarget} onChange={e => setBlockTarget(e.target.value)} /></label>
        <div className="safety-actions"><button className="btn btn-dark" disabled={busy}>Chặn</button>
          <button type="button" className="btn btn-outline-secondary" disabled={busy || !blockTarget} onClick={() => void changeBlock(null, 'unblock')}>Bỏ chặn</button></div>
      </form>
      <form className="safety-card" onSubmit={submitReport}>
        <h3>Gửi báo cáo</h3>
        <label>Đối tượng<select value={targetType} onChange={e => setTargetType(e.target.value as ReportType)}>
          <option value="User">Người dùng</option><option value="Message">Tin nhắn</option><option value="Post">Bài viết</option>
        </select></label>
        <label>ID đối tượng<input required maxLength={200} value={targetId} onChange={e => setTargetId(e.target.value)} /></label>
        <label>Lý do<select required value={reason} onChange={e => setReason(e.target.value)}>
          <option value="">Chọn lý do</option><option>Quấy rối</option><option>Lừa đảo</option><option>Nội dung không phù hợp</option><option>Khác</option>
        </select></label>
        <label>Mô tả<textarea required minLength={10} maxLength={2000} rows={3} value={description} onChange={e => setDescription(e.target.value)} /></label>
        <button className="btn btn-dark" disabled={busy}>Gửi báo cáo</button>
      </form>
    </div>
    <div className="admin-area">
      <div className="panel-heading"><div><p className="eyebrow">DÀNH CHO ADMIN</p><h3>Báo cáo đang chờ</h3></div>
        <button className="btn btn-outline-secondary" onClick={() => void loadReports()}>Tải lại</button></div>
      {reports.length === 0 ? <p>Không có báo cáo chờ hoặc tài khoản chưa được cấp quyền Admin.</p> :
        <ul className="report-list">{reports.map(report => <li key={report.id}>
          <button onClick={() => { setSelected(report); setReviewNotes(''); }}>
            <strong>{report.targetType} · {report.reason}</strong><span>{report.description}</span>
            <small>{new Date(report.createdAtUtc).toLocaleString('vi-VN')}</small>
          </button>
        </li>)}</ul>}
      {selected && <article className="report-detail">
        <h4>Chi tiết báo cáo</h4><p>Người gửi: {selected.reporterUserId}</p><p>Đối tượng: {selected.targetType} · {selected.targetId}</p>
        <p>{selected.description}</p><p className="safety-note">Màn hình này không tải nội dung chat. Nếu cần xem nội dung tin nhắn liên quan, phải qua quyền truy xuất và audit của module Chat.</p>
        <label>Ghi chú xử lý<input required minLength={5} maxLength={2000} value={reviewNotes} onChange={e => setReviewNotes(e.target.value)} /></label>
        <div className="safety-actions"><button className="btn btn-outline-secondary" disabled={busy} onClick={() => void updateReport('Dismissed')}>Bỏ qua</button>
          <button className="btn btn-dark" disabled={busy} onClick={() => void updateReport('Reviewed')}>Đã xem xét</button>
          {selected.targetUserId && <><input aria-label="Lý do khóa tài khoản" placeholder="Lý do khóa tài khoản" value={accountReason} onChange={e => setAccountReason(e.target.value)} />
            <button className="btn btn-danger" disabled={busy || accountReason.trim().length < 5} onClick={() => void setAccountActive(false)}>Khóa tài khoản</button>
            <button className="btn btn-outline-secondary" disabled={busy || accountReason.trim().length < 5} onClick={() => void setAccountActive(true)}>Mở tài khoản</button></>}
        </div>
      </article>}
    </div>
    <p aria-live="polite" className="safety-message">{message}</p>
  </section>;
}
