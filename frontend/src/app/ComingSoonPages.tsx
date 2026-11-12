export function WalletPage() {
  return <section className="feature-page wallet-page"><h1>Ví của tôi</h1><p className="page-lead">Xem trạng thái ví và lịch sử giao dịch.</p>
    <div className="wallet-layout"><article className="wallet-balance"><h2>Ví mylove</h2><p>Dịch vụ thanh toán đang được chuẩn bị.</p><div className="wallet-actions"><button disabled>Nạp tiền</button><button disabled>Rút tiền</button></div></article>
    <article className="ml-card"><h2>Lịch sử giao dịch</h2><p>Chưa có giao dịch.</p><a className="text-link" href="/chat">Tặng quà trong trò chuyện →</a></article></div></section>;
}
