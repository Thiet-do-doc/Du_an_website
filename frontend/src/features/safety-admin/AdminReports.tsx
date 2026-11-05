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
  if (error instanceof ApiError && error.status === 409) return 'Trạng thái đã thay đổi. Hãy tải lại để xem kết quả.';
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
  return <section className="safety-panel">
    <h1>Quản lý báo cáo</h1>
    <label>Trạng thái <select value={status} onChange={e => {
      setStatus(e.target.value as Status | ''); setPage(1); setSelected('');
    }}><option value="">Tất cả</option>{Object.entries(statusNames).map(([key, name]) =>
      <option key={key} value={key}>{name}</option>)}</select></label>
    <button className="btn btn-outline-secondary" onClick={() => { setRevision(n => n + 1); setSelected(''); }}>Tải lại</button>
    {error ? <p role="alert">{error}</p> : reports === null ? <p role="status">Đang tải báo cáo…</p> : <>
      {reports.length === 0 && <p>Chưa có báo cáo trên trang này.</p>}
      <ul className="report-list">{reports.map(row => <li key={row.id}>
        <button onClick={() => setSelected(row.id)} aria-pressed={selected === row.id}>
          <strong>{targetNames[row.targetType]} · {row.reason}</strong><small>{statusNames[row.status]} · {time(row.createdAtUtc)}</small>
        </button></li>)}</ul>
      <nav className="safety-actions" aria-label="Phân trang báo cáo">
        <button disabled={page === 1} onClick={() => { setSelected(''); setPage(n => n - 1); }}>Trang trước</button>
        <span>Trang {page}</span>
        <button disabled={reports.length < pageSize} onClick={() => { setSelected(''); setPage(n => n + 1); }}>Trang sau</button>
      </nav></>}
    {selected && <ReportDetail key={selected} id={selected} onReview={() => setRevision(n => n + 1)} />}
  </section>;
}

function ReportDetail({ id, onReview }: { id: string; onReview: () => void }) {
  const url = reportsUrl + '/' + id;
  const [report, setReport] = useState<Report | null>(null);
  const [actions, setActions] = useState<Audit[]>([]);
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState<string | null>(null);
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
    setError(''); setBusy(true);
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
    setReason(''); onReview();
  }
  async function account(isActive: boolean) {
    await patch('/api/v1/admin/users/' + report!.targetUserId + '/status', { reportId: id, isActive, reason: reason.trim() });
    setReason('');
  }
  return <article className="report-detail"><h2>Chi tiết báo cáo</h2>
    {error && <p role="alert">{error}</p>}
    {!report ? !error && <p role="status">Đang tải chi tiết…</p> : <>
      <p>{statusNames[report.status]} · {time(report.createdAtUtc)}</p>
      <p>Người gửi: {report.reporterUserId}</p><p>{targetNames[report.targetType]}: {report.targetId}</p>
      <p><strong>{report.reason}</strong></p><p>{report.description}</p>
      {report.targetType === 'Message' && (evidence !== null ? <blockquote>{evidence}</blockquote> :
        <button disabled={busy} onClick={() => void perform(async () => {
          const message = await authenticatedFetch<{ content: string }>(url + '/evidence', { method: 'POST' });
          setEvidence(message.content);
        })}>Xem tin nhắn được báo cáo</button>)}
      <label>Lý do xử lý<textarea value={reason} maxLength={2000} onChange={e => setReason(e.target.value)} /></label>
      <div className="safety-actions">
        {(report.status === 'Pending' || report.status === 'Reviewed') && (['Reviewed', 'Dismissed', 'Actioned'] as const).map(value =>
          <button key={value} disabled={busy || !reason.trim()} onClick={() => void perform(() => review(value))}>{statusNames[value]}</button>)}
        {report.targetUserId && [false, true].map(active => <button key={String(active)}
          disabled={busy || reason.trim().length < 5 || reason.trim().length > 500}
          onClick={() => void perform(() => account(active))}>{active ? 'Mở' : 'Khóa'} tài khoản</button>)}
      </div>
      <h3>Nhật ký xử lý</h3>
      {actions.length === 0 ? <p>Chưa có thao tác.</p> : <ol>{actions.map(a => <li key={a.id}>
        <strong>{actionNames[a.action] ?? a.action}</strong> · {time(a.createdAtUtc)} · {a.actorUserId}<p>{a.reason}</p>
      </li>)}</ol>}
    </>}
  </article>;
}
