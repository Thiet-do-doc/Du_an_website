import { useEffect, useState } from 'react';
import { ApiError, authenticatedFetch } from '../auth/api';

const statusNames = { Pending: 'Chờ xử lý', Reviewed: 'Đã xem xét', Dismissed: 'Đã bác bỏ', Actioned: 'Đã xử lý' };
type Status = keyof typeof statusNames;
type Report = {
  id: string; status: Status; targetType: 'User' | 'Message' | 'Post'; targetId: string;
  targetUserId: string | null; reporterUserId: string; reason: string; description: string; createdAtUtc: string;
};
type Audit = { id: string; action: string; actorUserId: string; reason: string; createdAtUtc: string };
const actionNames: Record<string, string> = {
  ReportReviewed: 'Xem xét báo cáo', ReportDismissed: 'Bác bỏ báo cáo', ReportActioned: 'Xử lý báo cáo',
  AccountSuspended: 'Khóa tài khoản', AccountReactivated: 'Mở tài khoản', MessageEvidenceRead: 'Xem bằng chứng',
};
const targetNames = { User: 'Người dùng', Message: 'Tin nhắn', Post: 'Bài viết' };
const reportsUrl = '/api/v1/admin/reports';
const pageSize = 25;
const time = (value: string) => new Date(value).toLocaleString('vi-VN');

function errorMessage(error: unknown) {
  if (error instanceof ApiError && error.status === 403) return 'Bạn không có quyền quản trị.';
  if (error instanceof ApiError && error.status === 409) return error.message || 'Trạng thái đã thay đổi. Hãy tải lại để xem kết quả.';
  return error instanceof Error ? error.message : 'Không thể kết nối với máy chủ.';
}

export default function AdminReports() {
  const [status, setStatus] = useState<Status | ''>('Pending');
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [reports, setReports] = useState<Report[] | null>(null);
  const [selected, setSelected] = useState('');
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setError(''); setReports(null);
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) query.set('status', status);
    authenticatedFetch<Report[]>(reportsUrl + '?' + query, { signal: controller.signal })
      .then(rows => { if (!controller.signal.aborted) setReports(rows); })
      .catch(cause => { if (!controller.signal.aborted) setError(errorMessage(cause)); });
    return () => controller.abort();
  }, [page, status, revision]);

  return <section className="safety-panel" aria-labelledby="admin-reports-heading">
    <h1 id="admin-reports-heading">Quản lý báo cáo</h1>
    <p>Xem xét báo cáo vi phạm từ người dùng, kiểm tra bằng chứng và xử lý tài khoản vi phạm.</p>
    <div className="safety-grid admin-reports-grid">
      <section className="safety-card" aria-label="Danh sách báo cáo">
        <div className="safety-card-head">
          <div>
            <h2>Danh sách báo cáo</h2>
            <p className="safety-note">Lọc theo trạng thái và chọn một báo cáo để xem chi tiết.</p>
          </div>
        </div>
        <label>Trạng thái <select value={status} onChange={e => {
          setStatus(e.target.value as Status | ''); setPage(1); setSelected('');
        }}><option value="">Tất cả</option>{Object.entries(statusNames).map(([key, name]) =>
          <option key={key} value={key}>{name}</option>)}</select></label>
        <div className="safety-actions">
          <button type="button" className="btn btn-outline-dark" onClick={() => { setRevision(n => n + 1); setSelected(''); }}>Tải lại</button>
        </div>
        {error ? <p role="alert" className="safety-error">{error}</p> : reports === null ? <p role="status" className="safety-message">Đang tải báo cáo…</p> : <>
          {reports.length === 0 && <p className="safety-note safety-message">Chưa có báo cáo trên trang này.</p>}
          <ul className="report-list">{reports.map(row => <li key={row.id}>
            <button type="button" onClick={() => setSelected(row.id)} aria-pressed={selected === row.id}>
              <strong>{targetNames[row.targetType]} · {row.reason}</strong>
              <small>{statusNames[row.status]} · {time(row.createdAtUtc)}</small>
            </button></li>)}</ul>
          <nav className="safety-actions safety-pagination" aria-label="Phân trang báo cáo">
            <button type="button" className="btn btn-outline-dark" disabled={page === 1} onClick={() => { setSelected(''); setPage(n => n - 1); }}>Trang trước</button>
            <span>Trang {page}</span>
            <button type="button" className="btn btn-outline-dark" disabled={reports.length < pageSize} onClick={() => { setSelected(''); setPage(n => n + 1); }}>Trang sau</button>
          </nav>
        </>}
      </section>

      <div className="admin-detail-col">
        {selected ? <ReportDetail key={selected} id={selected} onReview={() => setRevision(n => n + 1)} /> :
          <section className="safety-card">
            <h2>Chi tiết báo cáo</h2>
            <p className="safety-note">Chọn một báo cáo từ danh sách bên cạnh để xem thông tin chi tiết, đọc bằng chứng tin nhắn (nếu có) và cập nhật trạng thái xử lý.</p>
          </section>}
      </div>
    </div>
  </section>;
}

function ReportDetail({ id, onReview }: { id: string; onReview: () => void }) {
  const url = reportsUrl + '/' + id;
  const [report, setReport] = useState<Report | null>(null);
  const [actions, setActions] = useState<Audit[]>([]);
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([authenticatedFetch<Report>(url, { signal: controller.signal }),
      authenticatedFetch<Audit[]>(url + '/actions', { signal: controller.signal })])
      .then(([row, history]) => { if (!controller.signal.aborted) { setReport(row); setActions(history); } })
      .catch(cause => { if (!controller.signal.aborted) setError(errorMessage(cause)); });
    return () => controller.abort();
  }, [url]);

  async function perform(operation: () => Promise<void>) {
    setError(''); setNotice(''); setBusy(true);
    try {
      await operation();
      setActions(await authenticatedFetch<Audit[]>(url + '/actions'));
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  function patch<T>(path: string, body: object) {
    return authenticatedFetch<T>(path, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  }
  async function review(status: Status) {
    setReport(await patch<Report>(url, { status, notes: reason.trim() }));
    setReason(''); setNotice(`Đã chuyển trạng thái báo cáo sang "${statusNames[status]}".`); onReview();
  }
  async function account(isActive: boolean) {
    await patch('/api/v1/admin/users/' + report!.targetUserId + '/status', { reportId: id, isActive, reason: reason.trim() });
    setReason(''); setNotice(isActive ? 'Đã mở lại tài khoản người dùng.' : 'Đã khóa tài khoản người dùng.');
  }

  const availableTransitions = report && (report.status === 'Pending' || report.status === 'Reviewed')
    ? (['Reviewed', 'Dismissed', 'Actioned'] as const).filter(value => value !== report.status)
    : [];

  return <article className="safety-card report-detail">
    <h2>Chi tiết báo cáo</h2>
    {error && <p role="alert" className="safety-error">{error}</p>}
    {notice && <p role="status" className="profile-notice">{notice}</p>}
    {!report ? !error && <p role="status">Đang tải chi tiết…</p> : <>
      <p className="safety-note">{statusNames[report.status]} · {time(report.createdAtUtc)}</p>
      <p><strong>Người gửi:</strong> <code>{report.reporterUserId}</code></p>
      <p><strong>{targetNames[report.targetType]}:</strong> <code>{report.targetId}</code></p>
      {report.targetUserId && <p><a className="text-link" href={'/profile?user=' + encodeURIComponent(report.targetUserId)}>Xem hồ sơ người bị báo cáo ↗</a></p>}
      <div className="report-body-box">
        <p><strong>{report.reason}</strong></p>
        <p>{report.description}</p>
      </div>
      {report.targetType === 'Message' && (evidence !== null ? <blockquote className="report-evidence">{evidence}</blockquote> :
        <div className="safety-actions">
          <button type="button" className="btn btn-outline-dark" disabled={busy} onClick={() => void perform(async () => {
            const message = await authenticatedFetch<{ content: string }>(url + '/evidence', { method: 'POST' });
            setEvidence(message.content);
          })}>Xem tin nhắn được báo cáo</button>
        </div>)}
      <label>Lý do xử lý<textarea rows={3} value={reason} maxLength={2000} onChange={e => setReason(e.target.value)} placeholder="Nhập lý do xử lý (tối thiểu 5 ký tự nếu khóa/mở tài khoản)…" /></label>
      <div className="safety-actions">
        {availableTransitions.map(value =>
          <button key={value} type="button" className={value === 'Actioned' ? 'btn btn-dark' : 'btn btn-outline-dark'} disabled={busy || !reason.trim()} onClick={() => void perform(() => review(value))}>{statusNames[value]}</button>)}
        {report.targetUserId && [false, true].map(active => <button key={String(active)} type="button"
          className={active ? 'btn btn-outline-secondary' : 'btn btn-outline-dark'}
          disabled={busy || reason.trim().length < 5 || reason.trim().length > 500}
          onClick={() => void perform(() => account(active))}>{active ? 'Mở tài khoản' : 'Khóa tài khoản'}</button>)}
      </div>
      <h3 className="report-audit-title">Nhật ký xử lý</h3>
      {actions.length === 0 ? <p className="safety-note">Chưa có thao tác.</p> : <ol className="report-audit">{actions.map(a => <li key={a.id}>
        <strong>{actionNames[a.action] ?? a.action}</strong> · <small>{time(a.createdAtUtc)} · <code>{a.actorUserId}</code></small>
        <p>{a.reason}</p>
      </li>)}</ol>}
    </>}
  </article>;
}
