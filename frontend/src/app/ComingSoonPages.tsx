export function GiftsPage() {
  return <section className="feature-page"><p className="ml-kicker">MỘT CHÚT QUAN TÂM</p><h1>Gửi một món quà,<br /><em>thêm một nụ cười.</em></h1><p className="page-lead">Một góc nhỏ dành cho những điều dễ thương bạn muốn trao đi.</p>
    <div className="feature-notice"><span className="soon-pill">Sắp ra mắt</span><p>Quà tặng đang được chuẩn bị. Bạn chưa thể mua hoặc gửi quà lúc này.</p></div>
    <div className="gift-grid">{[['🌷', 'Một nhành hoa', 'Thay cho một lời chào.'], ['💌', 'Lời nhắn yêu thương', 'Gửi chút chân thành đến người ấy.'], ['🧸', 'Một cái ôm', 'Dịu dàng cho một ngày thật dài.']].map(([icon, name, copy]) => <article className="ml-card gift-card" key={name}><span className="gift-illustration" aria-hidden="true">{icon}</span><h2>{name}</h2><p>{copy}</p><button className="btn btn-outline-secondary" disabled>Chưa mở tặng quà</button></article>)}</div>
    <a className="text-link" href="/discover">Khám phá những kết nối mới ↗</a></section>;
}
export function WalletPage() {
  return <section className="feature-page wallet-page"><p className="ml-kicker">KHÔNG GIAN CỦA BẠN</p><h1>Ví của tôi<span className="pink-dot">.</span></h1><p className="page-lead">Quản lý số dư và những món quà trong tương lai.</p>
    <div className="wallet-layout"><article className="wallet-balance"><span>VÍ MYLOVE</span><h2>Chưa kích hoạt</h2><p>Tính năng ví sẽ xuất hiện khi dịch vụ thanh toán sẵn sàng.</p><div className="wallet-actions"><button disabled>Nạp tiền</button><button disabled>Rút tiền</button></div></article>
    <article className="ml-card"><span className="soon-pill">Sắp ra mắt</span><h2>Lịch sử giao dịch</h2><p>Chưa có giao dịch. Nạp tiền, rút tiền và quy đổi quà tặng hiện chưa được mở.</p><a className="text-link" href="/gifts">Ghé góc quà tặng ↗</a></article></div></section>;
}
