import { authenticatedFetch } from '../auth/api';
import { useAuth } from '../auth/AuthProvider';
import BlockButton from './BlockButton';
import { blockUser, getBlockedUsers } from './api/blocks';
import type { BlockedUser } from './types/block';
import { type FormEvent, useCallback, useEffect, useState } from 'react';

type Report = { id: string; reason: string; description: string; status: 'Pending' | 'Reviewed' | 'Dismissed' | 'Actioned'; createdAtUtc: string };
const statuses = { Pending: 'Chờ xử lý', Reviewed: 'Đã xem xét', Dismissed: 'Đã bác bỏ', Actioned: 'Đã xử lý' };
const uuidPattern = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export default function SafetyAdminPanel({ isAdmin = false }: { isAdmin?: boolean }) {
  const { session } = useAuth();
  const requestedUser = new URLSearchParams(location.search).get('user')?.trim() ?? '';
  const hasValidTargetUser = uuidPattern.test(requestedUser) && requestedUser.toLowerCase() !== session?.user.id.toLowerCase();

  const [targetType, setTargetType] = useState('User');
  const [targetId, setTargetId] = useState(requestedUser);
  const [showForm, setShowForm] = useState(Boolean(requestedUser));
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const [reports, setReports] = useState<Report[]>([]);
  const [page, setPage] = useState(1);
  const [more, setMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [version, setVersion] = useState(0);

  const [blocked, setBlocked] = useState<BlockedUser[]>([]);
  const [blocksLoading, setBlocksLoading] = useState(true);
  const [blocksError, setBlocksError] = useState('');
  const [showBlockForm, setShowBlockForm] = useState(false);
  const [manualBlockId, setManualBlockId] = useState('');

  const loadBlocked = useCallback(async () => {
    setBlocksLoading(true);
    setBlocksError('');
    try {
      setBlocked(await getBlockedUsers());
    } catch (e) {
      setBlocksError(e instanceof Error ? e.message : 'Chưa tải được danh sách chặn.');
    } finally {
      setBlocksLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadBlocked();
  }, [loadBlocked]);

  useEffect(() => {
    let active = true; setLoading(true); setError('');
    authenticatedFetch<{ items: Report[]; hasMore: boolean }>('/api/v1/reports/mine?page=' + page)
      .then(data => { if (active) { setReports(data.items); setMore(data.hasMore); } })
      .catch(e => { if (active) setError(e instanceof Error ? e.message : 'Chưa tải được báo cáo.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, version]);

  async function submitReport(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage(''); setError('');
    try {
      await authenticatedFetch('/api/v1/reports', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ targetType, targetId, reason, description }) });
      setTargetId(''); setReason(''); setDescription(''); setShowForm(false); setPage(1); setVersion(x => x + 1);
      setMessage('Đã gửi báo cáo. Nhóm kiểm duyệt sẽ xem xét.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Không gửi được báo cáo.'); }
    finally { setBusy(false); }
  }

  async function submitBlockById(event: FormEvent) {
    event.preventDefault();
    const id = manualBlockId.trim();
    if (!uuidPattern.test(id)) {
      setBlocksError('ID người dùng phải đúng định dạng UUID.');
      return;
    }
    setBusy(true); setBlocksError(''); setMessage('');
    try {
      await blockUser(id);
      setManualBlockId('');
      setShowBlockForm(false);
      setMessage('Đã chặn người dùng.');
      await loadBlocked();
      window.dispatchEvent(new Event('notifications-read'));
    } catch (e) {
      setBlocksError(e instanceof Error ? e.message : 'Không chặn được người dùng.');
    } finally {
      setBusy(false);
    }
  }

  const isTargetBlocked = hasValidTargetUser && blocked.some(b => b.userId.toLowerCase() === requestedUser.toLowerCase());

  return <section className="safety-panel" aria-labelledby="safety-heading">
    <p className="eyebrow">AN TOÀN CỘNG ĐỒNG</p><h1 id="safety-heading">Chặn &amp; Báo cáo</h1>
    <p>Quản lý danh sách người dùng đã chặn và theo dõi những báo cáo bạn đã gửi đến nhóm kiểm duyệt.</p>
    {isAdmin && <div className="safety-actions"><a className="btn btn-outline-dark" href="/admin">Mở trang Quản lý báo cáo (Admin) ↗</a></div>}
    {message && <p className="safety-message profile-notice" role="status">{message}</p>}
    {hasValidTargetUser && <div className="safety-card safety-quick-target">
      <div>
        <h2>Thao tác với tài khoản được chọn</h2>
        <p className="safety-note">Mã tài khoản: <code>{requestedUser}</code></p>
      </div>
      <div className="safety-actions">
        <BlockButton userId={requestedUser} blocked={isTargetBlocked} onChanged={async () => {
          setMessage(isTargetBlocked ? 'Đã bỏ chặn người dùng.' : 'Đã chặn người dùng.');
          await loadBlocked();
          window.dispatchEvent(new Event('notifications-read'));
        }} />
        <button type="button" className="btn btn-dark" onClick={() => setShowForm(true)}>Báo cáo tài khoản này</button>
      </div>
    </div>}

    <div className="safety-grid">
      <section className="safety-card" aria-labelledby="blocked-heading">
        <div className="safety-card-head">
          <div>
            <h2 id="blocked-heading">Người dùng đã chặn</h2>
            <p className="safety-note">Chặn ngăn tương tác hai chiều trên bảng tin, khám phá và trò chuyện.</p>
          </div>
        </div>
        <div className="safety-actions">
          <button type="button" className="btn btn-outline-dark" disabled={blocksLoading} onClick={() => void loadBlocked()}>Tải lại</button>
          <button type="button" className="btn btn-outline-secondary" aria-expanded={showBlockForm} onClick={() => setShowBlockForm(!showBlockForm)}>
            {showBlockForm ? 'Đóng' : 'Chặn bằng ID'}
          </button>
        </div>
        {showBlockForm && <form className="safety-inline-form" onSubmit={e => void submitBlockById(e)}>
          <label>ID người dùng cần chặn
            <input required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" value={manualBlockId} onChange={e => setManualBlockId(e.target.value)} />
          </label>
          <button className="btn btn-dark" disabled={busy || manualBlockId.trim().toLowerCase() === session?.user.id.toLowerCase()}>Chặn</button>
        </form>}
        {blocksError && <p role="alert" className="safety-error">{blocksError}</p>}
        {blocksLoading ? <p role="status">Đang tải danh sách chặn…</p> : !blocksError && (
          blocked.length === 0 ? <p className="safety-note">Bạn chưa chặn người dùng nào. Bạn có thể chặn trực tiếp từ hồ sơ, khám phá, bảng tin hoặc trò chuyện.</p> :
            <ul className="blocked-list">{blocked.map(item => <li key={item.userId} className="blocked-item">
              <div>
                <strong>{item.displayName || 'Thành viên mylove'}</strong>
                <small><code>{item.userId}</code> · {new Date(item.blockedAt).toLocaleString('vi-VN')}</small>
              </div>
              <BlockButton userId={item.userId} blocked onChanged={async () => {
                setMessage('Đã bỏ chặn người dùng.');
                await loadBlocked();
                window.dispatchEvent(new Event('notifications-read'));
              }} />
            </li>)}</ul>
        )}
      </section>

      <section className="safety-card" aria-labelledby="reports-heading">
        <div className="safety-card-head">
          <div>
            <h2 id="reports-heading">Báo cáo của bạn</h2>
            <p className="safety-note">Theo dõi tiến độ xử lý các báo cáo vi phạm bạn đã gửi.</p>
          </div>
        </div>
        <div className="safety-actions"><button type="button" className="btn btn-dark" aria-expanded={showForm} onClick={() => setShowForm(!showForm)}>{showForm ? 'Đóng biểu mẫu' : 'Gửi báo cáo'}</button>
          <button type="button" className="btn btn-outline-dark" disabled={loading} onClick={() => setVersion(x => x + 1)}>Tải lại</button></div>
        {showForm && <form className="safety-inline-form" onSubmit={submitReport}>
          <h3>Gửi báo cáo</h3>
          <label>Đối tượng<select value={targetType} onChange={e => setTargetType(e.target.value)}><option value="User">Người dùng</option><option value="Message">Tin nhắn</option></select></label>
          <label>ID đối tượng<input required value={targetId} onChange={e => setTargetId(e.target.value)} /></label>
          <label>Lý do<select required value={reason} onChange={e => setReason(e.target.value)}><option value="">Chọn lý do</option><option>Quấy rối</option><option>Lừa đảo</option><option>Nội dung không phù hợp</option><option>Khác</option></select></label>
          <label>Mô tả<textarea required minLength={10} maxLength={2000} rows={3} value={description} onChange={e => setDescription(e.target.value)} /></label>
          <button className="btn btn-dark" disabled={busy}>{busy ? 'Đang gửi…' : 'Gửi báo cáo'}</button>
        </form>}
        {error && <p role="alert" className="safety-error">{error}</p>}
        {loading ? <p role="status">Đang tải báo cáo…</p> : !error && <>
          {!reports.length && <p className="safety-note">Bạn chưa có báo cáo nào trên trang này.</p>}
          <ul className="report-list">{reports.map(report => <li className="report-card" key={report.id}><strong>{report.reason}</strong><p>{report.description}</p>
            <small>{statuses[report.status]} · {new Date(report.createdAtUtc).toLocaleString('vi-VN')}</small></li>)}</ul>
          <nav className="safety-actions safety-pagination" aria-label="Phân trang báo cáo">
            <button type="button" className="btn btn-outline-dark" disabled={page === 1} onClick={() => setPage(x => x - 1)}>Trang trước</button>
            <span>Trang {page}</span>
            <button type="button" className="btn btn-outline-dark" disabled={!more} onClick={() => setPage(x => x + 1)}>Trang sau</button>
          </nav>
        </>}
      </section>
    </div>
  </section>;
}
