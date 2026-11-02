export function WalletPage() {
  return <section className="feature-page wallet-page"><p className="ml-kicker">KHÔNG GIAN CỦA BẠN</p><h1>Ví của tôi<span className="pink-dot">.</span></h1><p className="page-lead">Quản lý số dư và những món quà trong tương lai.</p>
    <div className="wallet-layout"><article className="wallet-balance"><span>VÍ MYLOVE</span><h2>Chưa kích hoạt</h2><p>Tính năng ví sẽ xuất hiện khi dịch vụ thanh toán sẵn sàng.</p><div className="wallet-actions"><button disabled>Nạp tiền</button><button disabled>Rút tiền</button></div></article>
    <article className="ml-card"><span className="soon-pill">Sắp ra mắt</span><h2>Lịch sử giao dịch</h2><p>Chưa có giao dịch. Nạp tiền, rút tiền và quy đổi quà tặng hiện chưa được mở.</p><a className="text-link" href="/gifts">Ghé góc quà tặng ↗</a></article></div></section>;
}
