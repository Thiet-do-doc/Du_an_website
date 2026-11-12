import { useState } from 'react';
import { AuthPage, type AuthMode } from '../features/auth/AuthPages';
import { WelcomeMist } from './WelcomeMist';
import { NavIcon } from './NavIcon';

function Portrait({ index }: { index: number }) {
  const [available, setAvailable] = useState(true);
  return <div className={'polaroid polaroid-' + index}><div>
    {available ? <img src={'/images/member-' + (index + 1) + '.jpg'} alt={'Thành viên ' + (index + 1) + ' của mylove'} onError={() => setAvailable(false)} /> :
      <><b aria-hidden="true">{['M','Y','L','O','V'][index]}</b><span aria-hidden="true">♡</span></>}
  </div></div>;
}

export function Brand() {
  return <a className="ml-brand" href="/" aria-label="mylove · Trang chủ"><span className="brand-mark"><NavIcon name="heart" /></span><span>mylove<span className="brand-dot">.</span></span></a>;
}
export default function App({ mode = 'login' }: { mode?: AuthMode }) {
  return <div className="welcome-page">
    {(mode === 'login' || mode === 'register') && <WelcomeMist />}
    <header className="welcome-header"><Brand /></header>
    <main className="welcome-layout">
      <section className="welcome-story">
        <h2>Chào mừng bạn<br /> đến với <em>mylove.</em></h2>
        <p className="welcome-description">Gặp người mới và chia sẻ những điều bạn yêu thích.</p>
        <figure className="team-banner">
            <div className="team-placeholder" role="group" aria-label="Năm thành viên nhóm phát triển mylove">
              <div className="team-polaroids">{[0, 1, 2, 3, 4].map(i => <Portrait key={i} index={i} />)}</div>
            </div>
          <figcaption className="team-caption">Nhóm phát triển mylove</figcaption>
        </figure>
      </section>
      <div className="welcome-form"><AuthPage mode={mode} /></div>
    </main>
  </div>;
}
