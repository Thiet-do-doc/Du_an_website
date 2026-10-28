import type { ReactNode } from 'react';
import App, { Brand } from './App';
import { AccountPage, AdminPage } from '../features/auth/AuthPages';
import { useAuth } from '../features/auth/AuthProvider';
import SafetyAdminPanel from '../features/safety-admin/SafetyAdminPanel';
import BlocksPage from '../features/safety-admin/BlocksPage';
import AuthenticatedChatPage from '../features/chat-notifications/pages/AuthenticatedChatPage';
import ProfilePage from '../features/profiles/ProfilePage';
import DiscoveryPage from '../features/discovery-matching/pages/DiscoveryPage';
import FeedPage from '../features/posts/FeedPage';
import { GiftsPage, WalletPage } from './ComingSoonPages';

const tabs = [['/', 'Trang chủ', '⌂'], ['/discover', 'Khám phá', '✦'], ['/chat', 'Trò chuyện', '♡'], ['/gifts', 'Quà tặng', '♧'], ['/wallet', 'Ví của tôi', '▱'], ['/profile', 'Hồ sơ', '☺'], ['/safety', 'An toàn', '◇']] as const;
function MemberShell({ path, children }: { path: string; children: ReactNode }) {
  return <div className="member-app"><a className="skip-link" href="#main-content">Đến nội dung</a>
    <header className="member-header"><div className="member-header-inner"><Brand />
      <nav className="member-nav" aria-label="Điều hướng chính">{tabs.map(([href, label, icon]) => <a key={href} href={href} aria-current={path === href || href === '/safety' && path === '/blocks' ? 'page' : undefined}><span aria-hidden="true">{icon}</span>{label}</a>)}</nav>
      <a className="member-account" href="/account" aria-label="Tài khoản và đăng xuất" aria-current={path === '/account' ? 'page' : undefined}>Tài khoản <span aria-hidden="true">↗</span></a>
    </div></header>
    <main id="main-content" className="member-content">{children}</main>
    <footer className="member-footer">mylove <span>♡</span> Kết nối bằng sự chân thành.</footer>
  </div>;
}
export default function AppRouter() {
  const auth = useAuth();
  const path = location.pathname.replace(/\/$/, '') || '/';
  const modes = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email', 'resend-confirmation'] as const;
  const mode = modes.find(value => path === '/' + value);
  if (mode && !['login', 'register'].includes(mode)) return <App mode={mode} />;
  if (!auth.ready) return <div className="session-loading"><Brand /><p role="status">Đang mở mylove…</p></div>;
  if (!auth.session) return <App mode={mode ?? 'login'} />;
  const content = path === '/' || path === '/login' || path === '/register' ? <FeedPage /> :
    path === '/discover' ? <DiscoveryPage /> : path === '/chat' ? <AuthenticatedChatPage /> :
    path === '/profile' ? <ProfilePage /> : path === '/account' ? <AccountPage /> :
    path === '/admin' ? <AdminPage /> : path === '/safety' ? <SafetyAdminPanel /> :
    path === '/blocks' ? <BlocksPage /> : path === '/gifts' ? <GiftsPage /> : path === '/wallet' ? <WalletPage /> :
    <section className="ml-card empty-feature"><h1>Trang này chưa có ở mylove.</h1><a className="btn btn-dark" href="/">Về trang chủ</a></section>;
  return <MemberShell path={mode ? '/' : path}>{content}</MemberShell>;
}
