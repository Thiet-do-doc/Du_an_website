import { useEffect, useLayoutEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react';
import { NavIcon } from './NavIcon';
import { LoginWelcome } from './WelcomeMist';
import App, { Brand } from './App';
import { AccountPage, AdminPage } from '../features/auth/AuthPages';
import { useAuth } from '../features/auth/AuthProvider';
import SafetyAdminPanel from '../features/safety-admin/SafetyAdminPanel';
import AuthenticatedChatPage from '../features/chat-notifications/pages/AuthenticatedChatPage';
import ProfilePage from '../features/profiles/ProfilePage';
import MemberProfilePage from '../features/profiles/MemberProfilePage';
import DiscoveryPage from '../features/discovery-matching/pages/DiscoveryPage';
import FeedPage from '../features/posts/FeedPage';
import { WalletPage } from './ComingSoonPages';
import NotificationsPage, { NotificationBadge } from '../features/chat-notifications/pages/NotificationsPage';
import { authenticatedFetch } from '../features/auth/api';
import { getMyProfile } from '../features/profiles/api';
import type { Profile } from '../features/profiles/types';
import { getVisibleTabs, isAppRoutePath, resolveRouteDestination } from './navigation';

function navigate(event: MouseEvent<HTMLDivElement>) {
  if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
  const link = (event.target as Element).closest<HTMLAnchorElement>('a[href]');
  if (!link || link.target || link.hasAttribute('download') || link.origin !== location.origin || link.hash) return;
  if (!isAppRoutePath(link.pathname)) return;
  event.preventDefault();
  if (link.href === location.href) return;
  const switchingForms = link.closest('.welcome-form') && ['/login', '/register'].includes(link.pathname);
  history.pushState(null, '', link.href);
  window.dispatchEvent(new PopStateEvent('popstate'));
  if (!switchingForms) window.scrollTo(0, 0);
}

function MemberShell({ path, children, admin = false }: { path: string; children: ReactNode; admin?: boolean }) {
  const visibleTabs = getVisibleTabs(admin);
  const nav = useRef<HTMLElement>(null);
  const currentIndex = visibleTabs.findIndex(([href]) => href === path);
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
  }, [currentIndex, visibleTabs.length]);
  return <div className="member-app"><a className="skip-link" href="#main-content">Đến nội dung</a>
    <LoginWelcome />
    <header className="member-header"><div className="member-header-inner"><Brand />
      <nav ref={nav} className="member-nav" aria-label="Điều hướng chính"><i className="tab-highlight" hidden={currentIndex < 0} aria-hidden="true"/>{visibleTabs.map(([href, label, icon], i) => <a key={href} href={href} aria-current={currentIndex === i ? 'page' : undefined}><NavIcon name={icon}/>{label}{href === '/notifications' && <NotificationBadge />}</a>)}</nav>
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
  const [access, setAccess] = useState<{ userId: string; admin: boolean; complete: boolean } | null>(null);
  const [accessError, setAccessError] = useState('');
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const userId = auth.session?.user.id;
    if (!userId) { setAccess(null); return; }
    let active = true;
    setAccessError('');
    const check = async () => {
      try {
        const me = await authenticatedFetch<{ roles: string[] }>('/api/v1/auth/me');
        const admin = me.roles.includes('Admin');
        const complete = admin || (await getMyProfile()).isComplete;
        if (active) { setAccess({ userId, admin, complete }); setAccessError(''); }
      } catch { if (active) setAccessError('Chưa kiểm tra được tài khoản. Hãy thử lại.'); }
    };
    void check();
    const focus = () => { void check(); };
    window.addEventListener('focus', focus);
    const updated = (event: Event) => setAccess(old => old && ({ ...old, complete: old.admin || (event as CustomEvent<Profile>).detail.isComplete }));
    window.addEventListener('profile-updated', updated);
    return () => { active = false; window.removeEventListener('profile-updated', updated); window.removeEventListener('focus', focus); };
  }, [auth.session?.user.id, auth.session?.token, retry]);
  const path = route.split('?')[0].replace(/\/$/, '') || '/';
  const destination = resolveRouteDestination(access, auth.session?.user.id, path, location.search);
  useEffect(() => {
    if (!destination) return;
    history.replaceState(null, '', destination);
    window.dispatchEvent(new PopStateEvent('popstate'));
  }, [destination, route]);
  const modes = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email', 'resend-confirmation', 'verify-email'] as const;
  const mode = modes.find(value => path === '/' + value);
  if (mode && !['login', 'register'].includes(mode)) return <App mode={mode} />;
  if (!auth.ready) return <div className="session-loading"><Brand /><p role="status">Đang mở mylove…</p></div>;
  if (!auth.session) return <div onClick={navigate}><App mode={mode ?? 'login'} /></div>;
  if (accessError) return <section className="ml-card"><p role="alert">{accessError}</p><button onClick={() => setRetry(x => x + 1)}>Thử lại</button></section>;
  if (!access || access.userId !== auth.session.user.id || destination) return <p role="status">Đang mở tài khoản…</p>;
  const memberId = new URLSearchParams(location.search).get('user');
  const content = path === '/' || path === '/login' || path === '/register' ? <FeedPage /> :
    path === '/discover' ? <DiscoveryPage /> : path === '/chat' ? <AuthenticatedChatPage /> :
    path === '/profile' ? (memberId && memberId.toLowerCase() !== auth.session.user.id.toLowerCase()
      ? <MemberProfilePage key={memberId} userId={memberId} /> : <ProfilePage />) : path === '/account' ? <AccountPage /> :
    path === '/admin' ? (access.admin ? <AdminPage /> : <section className="ml-card empty-feature"><h1>Bạn không có quyền quản trị.</h1><p className="page-lead">Chỉ tài khoản Admin mới có thể truy cập trang quản lý báo cáo.</p><a className="btn btn-dark" href="/">Về trang chủ</a></section>) :
    path === '/safety' ? <SafetyAdminPanel isAdmin={access.admin} /> :
    path === '/notifications' ? <NotificationsPage /> : path === '/wallet' ? <WalletPage /> :
    <section className="ml-card empty-feature"><h1>Trang này chưa có ở mylove.</h1><a className="btn btn-dark" href="/">Về trang chủ</a></section>;
  return <div onClick={navigate}><MemberShell path={mode ? '/' : path} admin={access.admin}>{content}</MemberShell></div>;
}
