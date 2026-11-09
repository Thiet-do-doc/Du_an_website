import { useEffect, useState } from 'react';

export function WelcomeMist() {
  const [visible, setVisible] = useState(true);
  if (!visible) return null;
  return <div className="welcome-mist" aria-hidden="true" onAnimationEnd={e => {
    if (e.target === e.currentTarget) setVisible(false);
  }}>
    <div className="mist-cloud" />
    <div className="mist-greeting"><span>Chào mừng bạn đến với</span><strong>mylove<i>.</i></strong></div>
  </div>;
}

const loginWelcome = 'mylove.login-welcome';
export function finishSignIn(destination?: string) {
  sessionStorage.setItem(loginWelcome, '1');
  if (destination) { location.replace(destination); return; }
  const post = new URLSearchParams(location.search).get('post');
  if (location.pathname === '/profile' && new URLSearchParams(location.search).has('user')) {
    location.reload();
  } else {
    location.replace(post ? '/?post=' + encodeURIComponent(post) : '/');
  }
}
export function LoginWelcome() {
  const [show] = useState(() => sessionStorage.getItem(loginWelcome) === '1');
  useEffect(() => { sessionStorage.removeItem(loginWelcome); }, []);
  return show ? <WelcomeMist /> : null;
}
