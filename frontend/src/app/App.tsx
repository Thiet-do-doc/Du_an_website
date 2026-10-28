import { useState } from 'react';
import { AuthPage, type AuthMode } from '../features/auth/AuthPages';

export function Brand() {
  return <a className="ml-brand" href="/" aria-label="mylove · Trang chủ"><span aria-hidden="true">♡</span> mylove<span className="brand-dot">.</span></a>;
}
export default function App({ mode = 'login' }: { mode?: AuthMode }) {
  const [hasTeamPhoto, setHasTeamPhoto] = useState(true);
  return <div className="welcome-page">
    <header className="welcome-header"><Brand /><span>Một lời chào. Một kết nối mới.</span></header>
    <main className="welcome-layout">
      <section className="welcome-story">
        <p className="ml-kicker">NƠI NHỮNG CÂU CHUYỆN BẮT ĐẦU</p>
        <h1>Chào mừng bạn<br />đến với <em>mylove.</em></h1>
        <p className="welcome-description">Một chút chân thành, một chút rung động.<br />Gặp người mới và viết nên câu chuyện của riêng bạn.</p>
        <div className="team-banner">
          {hasTeamPhoto ? <img className="team-photo" src="/images/team.jpg" alt="Nhóm phát triển mylove" onError={() => setHasTeamPhoto(false)} /> :
            <div className="team-placeholder" aria-label="Vị trí dành cho ảnh kỷ niệm của nhóm phát triển">
              <span className="banner-spark" aria-hidden="true">✦</span>
              <div className="team-polaroids" aria-hidden="true">{['M', 'Y', 'L', 'O', 'V'].map((letter, i) => <div className={`polaroid polaroid-${i}`} key={letter}><div>{letter}<span>♡</span></div><small>mylove team</small></div>)}</div>
              <span className="banner-heart" aria-hidden="true">♡</span>
            </div>}
          <div className="team-caption"><span>FROM OUR TEAM, WITH LOVE</span><p>Năm người bạn. Một nơi để kết nối.</p></div>
        </div>
        <div className="welcome-values"><span>♡ Kết nối chân thành</span><span>✦ Là chính mình</span><span>⌁ Chia sẻ mỗi ngày</span></div>
      </section>
      <div className="welcome-form"><AuthPage key={mode} mode={mode} /><p className="welcome-footnote">Những kết nối đẹp bắt đầu từ một lời chào.</p></div>
    </main>
    <footer className="welcome-footer"><span>mylove · Made with love</span><span>Dành cho những câu chuyện của chúng mình ♡</span></footer>
  </div>;
}
