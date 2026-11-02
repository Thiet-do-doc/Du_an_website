import { useEffect, useLayoutEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react';
import { NavIcon } from './NavIcon';
import { LoginWelcome } from './WelcomeMist';
import App, { Brand } from './App';
import { AccountPage, AdminPage } from '../features/auth/AuthPages';
import { useAuth } from '../features/auth/AuthProvider';
import SafetyAdminPanel from '../features/safety-admin/SafetyAdminPanel';
import BlocksPage from '../features/safety-admin/BlocksPage';
import AuthenticatedChatPage from '../features/chat-notifications/pages/AuthenticatedChatPage';
import ProfilePage from '../features/profiles/ProfilePage';
import MemberProfilePage from '../features/profiles/MemberProfilePage';
import DiscoveryPage from '../features/discovery-matching/pages/DiscoveryPage';
import FeedPage from '../features/posts/FeedPage';
import { WalletPage } from './ComingSoonPages';
import GiftsPage from '../features/gifts/GiftsPage';

const tabs = [['/', 'Trang chủ', 'home'], ['/discover', 'Khám phá', 'compass'], ['/chat', 'Trò chuyện', 'chat'], ['/gifts', 'Quà tặng', 'gift'], ['/wallet', 'Ví của tôi', 'cash'], ['/profile', 'Hồ sơ', 'profile'], ['/safety', 'An toàn', 'shield']] as const;

function navigate(event: MouseEvent<HTMLDivElement>) {
  if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
  const link = (event.target as Element).closest<HTMLAnchorElement>('a[href]');
  if (!link || link.target || link.hasAttribute('download') || link.origin !== location.origin || link.hash) return;
  if (!tabs.some(([href]) => href === link.pathname) && !['/register', '/login', '/account', '/blocks', '/admin'].includes(link.pathname)) return;
  event.preventDefault();
  if (link.href === location.href) return;
  const switchingForms = link.closest('.welcome-form') && ['/login', '/register'].includes(link.pathname);
  history.pushState(null, '', link.href);
  window.dispatchEvent(new PopStateEvent('popstate'));
  if (!switchingForms) window.scrollTo(0, 0);
}

function MemberShell({ path, children }: { path: string; children: ReactNode }) {
  const nav = useRef<HTMLElement>(null);
  const currentIndex = tabs.findIndex(([href]) => href === (path === '/blocks' ? '/safety' : path));
  const previous = useRef(currentIndex);
  const direction = currentIndex < previous.current ? 'back' : 'next';
  useLayoutEffect(() => {
    previous.current = currentIndex;
    const bar = nav.current!;
    const active = bar.querySelector<HTMLAnchorElement>('[aria-current]');
    if (!active) return;
    const position = () => {
      bar.style.setProperty('--tab-x', active.offsetLeft + 'px');
      bar.style.setProperty('--tab-width', active.offsetWidth + 'px');
      bar.style.setProperty('--tab-height', active.offsetHeight + 'px');
    };
    position();
    const observer = new ResizeObserver(position);
    observer.observe(bar);
    observer.observe(active);
    if (bar.scrollWidth > bar.clientWidth) bar.scrollLeft = active.offsetLeft - (bar.clientWidth - active.offsetWidth) / 2;
    return () => observer.disconnect();
  }, [currentIndex]);
  return <div className="member-app"><a className="skip-link" href="#main-content">Đến nội dung</a>
    <LoginWelcome />
    <header className="member-header"><div className="member-header-inner"><Brand />
      <nav ref={nav} className="member-nav" aria-label="Điều hướng chính"><i className="tab-highlight" hidden={currentIndex < 0} aria-hidden="true"/>{tabs.map(([href, label, icon], i) => <a key={href} href={href} aria-current={currentIndex === i ? 'page' : undefined}><NavIcon name={icon}/>{label}</a>)}</nav>
      <a className="member-account" href="/account" aria-label="Tài khoản và đăng xuất" aria-current={path === '/account' ? 'page' : undefined}>Tài khoản <span aria-hidden="true">↗</span></a>
    </div></header>
    <main key={path + location.search} id="main-content" className={'member-content page-' + direction}>{children}</main>
    <footer className="member-footer">mylove <span>♡</span> Kết nối bằng sự chân thành.</footer>
  </div>;
}
export default function AppRouter() {
  const [route, setRoute] = useState(location.pathname + location.search);
  useEffect(() => {
    const update = () => setRoute(location.pathname + location.search);
    window.addEventListener('popstate', update);
    return () => window.removeEventListener('popstate', update);
  }, []);
  const auth = useAuth();
  const path = route.split('?')[0].replace(/\/$/, '') || '/';
  const modes = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email', 'resend-confirmation', 'verify-email'] as const;
  const mode = modes.find(value => path === '/' + value);
  if (mode && !['login', 'register'].includes(mode)) return <App mode={mode} />;
  if (!auth.ready) return <div className="session-loading"><Brand /><p role="status">Đang mở mylove…</p></div>;
  if (!auth.session) return <div onClick={navigate}><App mode={mode ?? 'login'} /></div>;
  const memberId = new URLSearchParams(location.search).get('user');
  const content = path === '/' || path === '/login' || path === '/register' ? <FeedPage /> :
    path === '/discover' ? <DiscoveryPage /> : path === '/chat' ? <AuthenticatedChatPage /> :
    path === '/profile' ? (memberId && memberId.toLowerCase() !== auth.session.user.id.toLowerCase()
      ? <MemberProfilePage key={memberId} userId={memberId} /> : <ProfilePage />) : path === '/account' ? <AccountPage /> :
    path === '/admin' ? <AdminPage /> : path === '/safety' ? <SafetyAdminPanel /> :
    path === '/blocks' ? <BlocksPage /> : path === '/gifts' ? <GiftsPage /> : path === '/wallet' ? <WalletPage /> :
    <section className="ml-card empty-feature"><h1>Trang này chưa có ở mylove.</h1><a className="btn btn-dark" href="/">Về trang chủ</a></section>;
  return <div onClick={navigate}><MemberShell path={mode ? '/' : path}>{content}</MemberShell></div>;
}
