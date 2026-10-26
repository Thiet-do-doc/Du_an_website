import SafetyAdminPanel from '../features/safety-admin/SafetyAdminPanel';
import AuthenticatedChatPage from '../features/chat-notifications/pages/AuthenticatedChatPage';
import BlocksPage from '../features/safety-admin/BlocksPage';
﻿import App from './App';
import { AccountPage, AdminPage, AuthPage } from '../features/auth/AuthPages';
import ProfilePage from '../features/profiles/ProfilePage';
import DiscoveryPage from '../features/discovery-matching/pages/DiscoveryPage';
import { ProtectedRoute } from '../features/auth/AuthProvider';
export default function AppRouter() {
  const path = location.pathname.replace(/\/$/, '') || '/';
  const modes = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email', 'resend-confirmation'] as const;
  const mode = modes.find(value => path === '/' + value);
  const content = path === '/safety' ? <ProtectedRoute><SafetyAdminPanel /></ProtectedRoute> : mode ? <AuthPage mode={mode} /> : path === '/blocks' ? <BlocksPage /> : path === '/account' ? <AccountPage /> : path === '/admin' ? <AdminPage /> : path === '/profile' ? <ProfilePage /> : path === '/discover' ? <ProtectedRoute><DiscoveryPage onBack={() => { window.location.href = '/'; }} /></ProtectedRoute> : null;
  if (path === '/') return <App />;
  if (path === '/chat') return <AuthenticatedChatPage />;
  return <><header className="topbar"><div className="container app-width"><a className="brand" href="/">Dating<span>App</span></a><nav className="d-flex flex-wrap gap-3 mt-2"><a href="/profile">Hồ sơ</a><a href="/discover">Khám phá</a><a href="/chat">Trò chuyện</a><a href="/safety">An toàn</a><a href="/account">Tài khoản</a></nav></div></header>
    <main className="container app-width py-5">{content ?? <><h1>Không tìm thấy trang</h1><a href="/">Về trang chủ</a></>}</main></>;
}
