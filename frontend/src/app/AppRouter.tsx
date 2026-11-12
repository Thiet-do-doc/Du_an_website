import { useEffect, useState, type MouseEvent, type ReactNode } from 'react';
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
import { AccountAvatar } from '../features/profiles/AccountAvatar';
import { usePrivatePhoto } from '../features/profiles/PrivatePhoto';
import type { Profile } from '../features/profiles/types';
import { getVisibleTabs, isAppRoutePath, resolveRouteDestination, type NavTab } from './navigation';

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

function MemberShell({ path, children, admin, name, avatar }: {
  path: string; children: ReactNode; admin: boolean; name?: string; avatar?: string;
}) {
  const tabs = getVisibleTabs(admin);
  const [moreOpen, setMoreOpen] = useState(false);
  useEffect(() => setMoreOpen(false), [path]);
  const mobileTabs = tabs.filter(([href]) => ['/', '/discover', '/chat', '/profile'].includes(href));
  const menuTabs = tabs.filter(tab => !mobileTabs.includes(tab));
  const renderTab = ([href, label, icon]: NavTab) => <a key={href} className="shell-link" href={href}
    aria-current={path === href ? 'page' : undefined} onClick={() => setMoreOpen(false)}>
    <NavIcon name={icon} /><span>{label}</span>
  </a>;
  return <div className="member-app member-workspace"><a className="skip-link" href="#main-content">Đến nội dung</a>
    <LoginWelcome />
    <aside className="workspace-sidebar">
      <Brand />
      <nav aria-label="Điều hướng chính">
        <div className="workspace-links">{tabs.slice(0, 4).map(renderTab)}</div>
        <div className="workspace-links workspace-personal"><p>Cá nhân</p>{tabs.slice(4).map(renderTab)}</div>
      </nav>
      <a className="shell-link workspace-account" href="/account" aria-current={path === '/account' ? 'page' : undefined}>
        <AccountAvatar source={avatar} />
        <span className="workspace-account-label">{name && <strong>{name}</strong>}<span>Tài khoản</span></span>
      </a>
    </aside>
    <div className="workspace-body">
      <header className="workspace-bar">
        <div className="mobile-brand"><Brand /></div>
        <a className="workspace-icon" href="/notifications" aria-label="Thông báo">
          <NavIcon name="bell" /><NotificationBadge />
        </a>
        <a className="workspace-icon workspace-profile-icon" href="/account" aria-label="Tài khoản"><AccountAvatar source={avatar} /></a>
      </header>
      <main key={path + location.search} id="main-content" className="member-content">{children}</main>
    </div>
    <nav className="workspace-mobile-nav" aria-label="Điều hướng trên điện thoại">
      {mobileTabs.map(renderTab)}
      <button type="button" className="shell-link" aria-expanded={moreOpen} aria-controls={moreOpen ? 'mobile-more-links' : undefined}
        onClick={() => setMoreOpen(open => !open)} onKeyDown={event => { if (event.key === 'Escape') setMoreOpen(false); }}>
        <NavIcon name="menu" /><span>Thêm</span>
      </button>
      {moreOpen && <div id="mobile-more-links" className="workspace-mobile-more"
        onKeyDown={event => {
          if (event.key !== 'Escape') return;
          event.currentTarget.parentElement?.querySelector('button')?.focus();
          setMoreOpen(false);
        }}>{menuTabs.map(renderTab)}</div>}
    </nav>
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
  const [access, setAccess] = useState<{ userId: string; admin: boolean; complete: boolean; profile?: Profile } | null>(null);
  const [accessError, setAccessError] = useState('');
  const [retry, setRetry] = useState(0);
  const profile = access?.userId === auth.session?.user.id ? access?.profile : undefined;
  const avatar = usePrivatePhoto(profile?.photos.find(photo => photo.isAvatar)?.url);
  useEffect(() => {
    const userId = auth.session?.user.id;
    if (!userId) { setAccess(null); return; }
    let active = true;
    let revision = 0;
    setAccessError('');
    const check = async () => {
      const request = ++revision;
      try {
        const me = await authenticatedFetch<{ roles: string[] }>('/api/v1/auth/me');
        if (!active || request !== revision) return;
        const admin = me.roles.includes('Admin');
        if (admin) {
          setAccess(old => ({ userId, admin, complete: true, profile: old?.userId === userId ? old.profile : undefined }));
          setAccessError('');
        }
        const currentProfile = await getMyProfile().catch(error => {
          if (!admin) throw error;
          return undefined;
        });
        if (active && request === revision && currentProfile) {
          setAccess({ userId, admin, complete: admin || currentProfile.isComplete, profile: currentProfile });
          setAccessError('');
        }
      } catch { if (active && request === revision) setAccessError('Chưa kiểm tra được tài khoản. Hãy thử lại.'); }
    };
    void check();
    const focus = () => { void check(); };
    window.addEventListener('focus', focus);
    const updated = (event: Event) => {
      const updatedProfile = (event as CustomEvent<Profile>).detail;
      if (updatedProfile.userId !== userId) return;
      revision++;
      setAccess(old => old?.userId === userId
        ? { ...old, complete: old.admin || updatedProfile.isComplete, profile: updatedProfile }
        : old);
    };
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
  const content = path === '/' || path === '/login' || path === '/register' ? <FeedPage avatar={avatar} /> :
    path === '/discover' ? <DiscoveryPage /> : path === '/chat' ? <AuthenticatedChatPage /> :
    path === '/profile' ? (memberId && memberId.toLowerCase() !== auth.session.user.id.toLowerCase()
      ? <MemberProfilePage key={memberId} userId={memberId} /> : <ProfilePage />) : path === '/account' ? <AccountPage avatar={avatar} name={profile?.displayName} /> :
    path === '/admin' ? (access.admin ? <AdminPage /> : <section className="ml-card empty-feature"><h1>Bạn không có quyền quản trị.</h1><p className="page-lead">Chỉ tài khoản Admin mới có thể truy cập trang quản lý báo cáo.</p><a className="btn btn-dark" href="/">Về trang chủ</a></section>) :
    path === '/safety' ? <SafetyAdminPanel isAdmin={access.admin} /> :
    path === '/notifications' ? <NotificationsPage /> : path === '/wallet' ? <WalletPage /> :
    <section className="ml-card empty-feature"><h1>Trang này chưa có ở mylove.</h1><a className="btn btn-dark" href="/">Về trang chủ</a></section>;
  return <div onClick={navigate}><MemberShell path={mode ? '/' : path} admin={access.admin} name={profile?.displayName} avatar={avatar}>{content}</MemberShell></div>;
}
