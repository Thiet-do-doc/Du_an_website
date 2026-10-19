import { useEffect, useState, type FormEvent } from 'react';
import { ApiError, authenticatedFetch, publicPost } from './api';
import { ProtectedRoute, useAuth } from './AuthProvider';
type Mode = 'login' | 'register' | 'forgot-password' | 'reset-password' | 'confirm-email' | 'resend-confirmation';
const titles: Record<Mode, string> = { login: 'Đăng nhập', register: 'Tạo tài khoản', 'forgot-password': 'Quên mật khẩu',
  'reset-password': 'Đặt lại mật khẩu', 'confirm-email': 'Xác nhận email', 'resend-confirmation': 'Gửi lại email xác nhận' };
export function AuthPage({ mode }: { mode: Mode }) {
  const auth = useAuth();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const params = new URLSearchParams(location.search);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage(''); setError(''); setFieldErrors({});
    const values = Object.fromEntries(new FormData(event.currentTarget).entries());
    try {
      if (mode === 'login') { await auth.login(String(values.email), String(values.password)); location.assign('/account'); }
      else {
        const result = await publicPost<{ message: string }>(mode,
          mode === 'confirm-email' ? { userId: params.get('userId'), token: params.get('token') } :
          mode === 'reset-password' ? { ...values, token: params.get('token') } : values);
        setMessage(result.message);
      }
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'Không thể kết nối máy chủ. Hãy thử lại.');
      if (cause instanceof ApiError) setFieldErrors(cause.problem.errors ?? {});
    } finally { setBusy(false); }
  }
  const password = ['login', 'register', 'reset-password'].includes(mode);
  const confirmation = ['register', 'reset-password'].includes(mode);
  function errors(name: string) {
    const matching = Object.entries(fieldErrors).filter(([key]) => key.toLowerCase().endsWith(name.toLowerCase()));
    return matching.flatMap(([, values]) => values).map((value, index) => <div className="text-danger small" key={index}>{value}</div>);
  }
  return <section className="auth-panel"><h1>{titles[mode]}</h1><form onSubmit={submit}>
    {mode !== 'confirm-email' && <label>Email<input name="email" type="email" autoComplete="email" required maxLength={256}
      defaultValue={mode === 'reset-password' ? params.get('email') ?? '' : ''} />{errors('email')}</label>}
    {password && <label>Mật khẩu<input name="password" type="password" required minLength={mode === 'login' ? 1 : 10}
      maxLength={128} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} />{errors('password')}</label>}
    {confirmation && <label>Nhập lại mật khẩu<input name="confirmPassword" type="password" required minLength={10} maxLength={128}
      autoComplete="new-password" />{errors('confirmPassword')}<small>Mật khẩu từ 10 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.</small></label>}
    {mode === 'register' && <label>Ngày sinh<input name="dateOfBirth" type="date" required />{errors('dateOfBirth')}<small>Bạn phải đủ 18 tuổi.</small></label>}
    {mode === 'confirm-email' && <p>Bấm nút để xác nhận địa chỉ email của bạn.</p>}
    <button className="btn btn-dark" disabled={busy}>{busy ? 'Đang xử lý…' : titles[mode]}</button>
    {message && <p className="text-success mt-3" role="status">{message}</p>}
    {error && <div className="text-danger mt-3" role="alert"><p>{error}</p>
      {Object.entries(fieldErrors).map(([key, values]) => <p key={key}>{values.join(' ')}</p>)}</div>}
  </form><nav className="auth-links"><a href="/login">Đăng nhập</a><a href="/register">Đăng ký</a>
    <a href="/forgot-password">Quên mật khẩu</a><a href="/resend-confirmation">Gửi lại xác nhận</a></nav></section>;
}
function AccountContent() {
  const auth = useAuth();
  const [me, setMe] = useState<{ email: string; userId: string; roles: string[] }>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => { void authenticatedFetch<typeof me>('/api/v1/auth/me').then(setMe).catch(e => setError(e.message)); }, []);
  async function signOut() {
    setBusy(true);
    try { await auth.logout(); location.assign('/login'); } catch { setError('Chưa đăng xuất được trên máy chủ. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  return <section className="auth-panel"><h1>Tài khoản của bạn</h1>
    {me ? <><p>{me.email}</p><p>Mã tài khoản: <code>{me.userId}</code></p>
      <p>Vai trò: {me.roles.length ? me.roles.join(', ') : 'Người dùng'}</p></> : <p role="status">Đang tải tài khoản…</p>}
    {error && <p role="alert">{error}</p>}
    <button className="btn btn-dark" disabled={busy} onClick={() => { void signOut(); }}>Đăng xuất</button>
    <nav className="auth-links"><a href="/admin">Kiểm tra quyền quản trị</a><a href="/">Về trang chủ</a></nav></section>;
}
export function AccountPage() { return <ProtectedRoute><AccountContent /></ProtectedRoute>; }
function AdminContent() {
  const [message, setMessage] = useState('Đang kiểm tra quyền…');
  useEffect(() => { void authenticatedFetch('/api/v1/auth/admin-check').then(() => setMessage('Bạn có quyền quản trị.'))
    .catch(error => setMessage(error instanceof ApiError && error.status === 403 ? 'Bạn không có quyền quản trị.' : error.message)); }, []);
  return <section className="auth-panel"><h1>Quyền quản trị</h1><p role="status">{message}</p><a href="/account">Về tài khoản</a></section>;
}
export function AdminPage() { return <ProtectedRoute><AdminContent /></ProtectedRoute>; }
