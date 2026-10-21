import App from './App';
import { AccountPage, AdminPage, AuthPage } from '../features/auth/AuthPages';
import AuthenticatedChatPage from '../features/chat-notifications/pages/AuthenticatedChatPage';
export default function AppRouter() {
  const path = location.pathname.replace(/\/$/, '') || '/';
  const modes = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email', 'resend-confirmation'] as const;
  const mode = modes.find(value => path === '/' + value);
  const content = mode ? <AuthPage mode={mode} /> : path === '/account' ? <AccountPage /> : path === '/admin' ? <AdminPage /> : null;
  if (path === '/') return <App />;
  if (path === '/chat') return <AuthenticatedChatPage />;
  return <><header className="topbar"><div className="container app-width"><a className="brand" href="/">Dating<span>App</span></a></div></header>
    <main className="container app-width py-5">{content ?? <><h1>Không tìm thấy trang</h1><a href="/">Về trang chủ</a></>}</main></>;
}
