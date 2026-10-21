import { useState } from 'react';
import { getHealth, type HealthResponse } from '../shared/api/health';

type Check = { state: 'idle' | 'loading' | 'done' | 'error'; data?: HealthResponse };
const modules = [
  ['01', 'Tài khoản & xác thực', 'TV1', 'Auth', 'TV2'],
  ['02', 'Hồ sơ & hình ảnh', 'TV2', 'Profiles', 'TV3'],
  ['03', 'Khám phá & ghép đôi', 'TV3', 'DiscoveryMatching', 'TV4'],
  ['04', 'Chat & thông báo', 'TV4', 'ChatNotifications', 'TV5'],
  ['05', 'An toàn & quản trị', 'TV5', 'SafetyAdmin', 'TV1'],
];

export default function App() {
  const [api, setApi] = useState<Check>({ state: 'idle' });
  const [db, setDb] = useState<Check>({ state: 'idle' });
  const busy = api.state === 'loading' || db.state === 'loading';

  async function check() {
    setApi({ state: 'loading' }); setDb({ state: 'loading' });
    await Promise.all([
      getHealth('/api/v1/health')
        .then(data => setApi({ state: 'done', data }))
        .catch(() => setApi({ state: 'error' })),
      getHealth('/api/v1/health/database')
        .then(data => setDb({ state: 'done', data }))
        .catch(() => setDb({ state: 'error' })),
    ]);
  }

  function statusCard(title: string, value: Check) {
    const good = value.data?.status === 'ok' || value.data?.status === 'connected';
    const text = value.state === 'idle' ? 'Chưa kiểm tra' : value.state === 'loading'
      ? 'Đang kiểm tra…' : value.state === 'error'
      ? 'Chưa gọi được API. Kiểm tra terminal backend và cổng 5080.' : value.data?.message;
    return <div className="status-card">
      <h3>{title}</h3>
      <p aria-live="polite" className={good ? 'status-good' : ''}>{text}</p>
      {value.data && <small>Kiểm tra lúc {new Date(value.data.checkedAt).toLocaleTimeString('vi-VN')}</small>}
    </div>;
  }

  return <>
    <header className="topbar"><div className="container app-width d-flex justify-content-between align-items-center">
      <span className="brand">Dating<span>App</span></span><div className="d-flex gap-3"><a href="/blocks">Chặn người dùng</a><a href="/profile">Hồ sơ</a><a href="/account">Tài khoản</a></div>
    </div></header>
    <main className="container app-width">
      <section className="intro">
        <p className="eyebrow">MỘT DỰ ÁN CHUNG · NĂM THÀNH VIÊN</p>
        <h1>Bắt đầu từ một<br /><span>nền tảng chung.</span></h1>
        <p className="intro-copy">React + TypeScript ở giao diện. ASP.NET Core ở backend. PostgreSQL lưu dữ liệu. Cả nhóm cùng phát triển từng chức năng từ đầu đến cuối.</p>
      </section>
      <section className="connection-panel" aria-labelledby="connection-heading">
        <div className="panel-heading"><div><p className="eyebrow">BƯỚC ĐẦU TIÊN</p><h2 id="connection-heading">Kiểm tra môi trường</h2></div>
          <button className="btn btn-dark check-button" onClick={check} disabled={busy}>{busy ? 'Đang kết nối…' : 'Kiểm tra kết nối'}</button>
        </div>
        <div className="status-grid">{statusCard('01 / Backend API', api)}{statusCard('02 / PostgreSQL', db)}</div>
        <p className="hint">Backend có thể chạy trước khi cấu hình database. Xem README.md để hoàn tất kết nối PostgreSQL.</p>
      </section>
      <section className="module-section" aria-labelledby="module-heading">
        <div className="section-heading"><h2 id="module-heading">Phần việc của nhóm</h2><span>Các module đang chờ triển khai</span></div>
        <div className="module-grid">{modules.map(([id, title, owner, folder, backup]) =>
          <article className="module-card" key={id}>
            <div className="d-flex justify-content-between"><span className="module-number">{id}</span><span className="owner">{owner}</span></div>
            <h3>{title}</h3><code>{folder}</code><p>Người dự phòng: {backup}</p>
          </article>)}</div>
      </section>
      <section className="next-step"><span className="next-marker">TÀI KHOẢN</span><div><h2>Đăng ký và đăng nhập</h2><p>Tạo tài khoản, xác nhận email và đăng nhập để sử dụng các chức năng tiếp theo.</p><a href="/register">Đăng ký</a> · <a href="/login">Đăng nhập</a></div></section>
    </main><footer className="container app-width">Đọc README.md để chạy dự án · docs/PHAN_CONG.md để nhận việc · docs/GIT_WORKFLOW.md để gửi PR</footer>
  </>;
}
