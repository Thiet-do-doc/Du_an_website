import ReportForm from './components/ReportForm';
import { authenticatedFetch } from '../auth/api';
import { type FormEvent, useCallback, useEffect, useState } from 'react';

type ReportType = 'User' | 'Message' | 'Post';
type ReportStatus = 'Pending' | 'Reviewed' | 'Dismissed' | 'Actioned';
type Report = {
  id: string; reporterUserId: string; targetType: ReportType; targetId: string;
  targetUserId?: string | null; reason: string; description: string; status: ReportStatus;
  reviewedByUserId?: string | null; reviewNotes?: string | null; createdAtUtc: string;
  reviewedAtUtc?: string | null;
};

const request = <T,>(path: string, init: RequestInit = {}) => authenticatedFetch<T>(path, {
  ...init, headers: { 'Content-Type': 'application/json', ...init.headers },
});

export default function SafetyAdminPanel() {
  const [blockTarget, setBlockTarget] = useState(new URLSearchParams(location.search).get('user') ?? '');
  const [isAdmin, setIsAdmin] = useState(false);
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
  useEffect(() => { void request<{ roles: string[] }>('/api/v1/auth/me').then(me => setIsAdmin(me.roles.includes('Admin'))).catch(() => setIsAdmin(false)); }, []);
  useEffect(() => { if (isAdmin) void loadReports(); }, [isAdmin, loadReports]);

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
      setAccountReason(''); setMessage(isActive ? 'Đã mở tài khoản.' : 'Đã khóa tài khoản và thu hồi các phiên đăng nhập.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không cập nhật được tài khoản.'); }
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
      <ReportForm key={location.search} targetType="User" targetId={new URLSearchParams(location.search).get('user') ?? ''} allowTargetSelection />
    </div>
    {isAdmin && <div className="admin-area">
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
    </div>}
    <p aria-live="polite" className="safety-message">{message}</p>
  </section>;
}
