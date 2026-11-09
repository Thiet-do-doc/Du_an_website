import { useEffect, useState, type FormEvent } from 'react';
import { ApiError, publicPost, verifyEmailCode } from './api';
import { finishSignIn } from '../../app/WelcomeMist';

const emailKey = 'mylove.verification.email';
const resendKey = 'mylove.verification.resendAt';
const nextResend = () => Date.now() + 60_000;

export function startEmailVerification(email: string) {
  sessionStorage.setItem(emailKey, email);
  sessionStorage.setItem(resendKey, String(nextResend()));
  location.assign('/verify-email');
}

export function EmailCodePage() {
  const email = sessionStorage.getItem(emailKey);
  const [code, setCode] = useState('');
  const [resendAt, setResendAt] = useState(() => Number(sessionStorage.getItem(resendKey)) || 0);
  const [now, setNow] = useState(Date.now);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const seconds = Math.max(0, Math.ceil((resendAt - now) / 1000));
  const waiting = seconds > 0;
  useEffect(() => {
    if (!waiting) return;
    const timer = window.setInterval(() => setNow(Date.now()), 500);
    return () => window.clearInterval(timer);
  }, [waiting]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!email || busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await verifyEmailCode(email, code);
      sessionStorage.removeItem(emailKey); sessionStorage.removeItem(resendKey);
      finishSignIn('/profile');
    } catch (e) { setError(e instanceof ApiError ? e.message : 'Không thể xác nhận. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  async function resend() {
    if (!email || busy || waiting) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await publicPost('resend-confirmation', { email });
      const deadline = nextResend();
      sessionStorage.setItem(resendKey, String(deadline));
      setNow(Date.now()); setResendAt(deadline); setCode('');
      setNotice('Hãy kiểm tra thư mới nhất để lấy mã xác nhận.');
    } catch (e) { setError(e instanceof ApiError ? e.message : 'Chưa gửi lại được mã. Hãy thử lại.'); }
    finally { setBusy(false); }
  }
  return <section className="auth-panel">
    <span className="auth-heart" aria-hidden="true">♡</span><h1>Xác nhận email</h1>
    {email ? <>
      <p>Nhập mã 6 chữ số trong email gửi tới <strong>{email}</strong>. Mã có hiệu lực 5 phút.</p>
      <form onSubmit={submit}>
        <label>Mã xác nhận<input name="code" type="text" inputMode="numeric" autoComplete="one-time-code"
          autoFocus required pattern="[0-9]{6}" maxLength={6} value={code} onChange={e => setCode(e.target.value)} /></label>
        <button className="btn btn-dark auth-submit" disabled={busy || !/^[0-9]{6}$/.test(code)}>Xác nhận và đăng nhập</button>
      </form>
      {error && <p role="alert" className="text-danger mt-3">{error}</p>}
      {notice && <p role="status" className="auth-success">{notice}</p>}
      <button type="button" className="btn btn-link" disabled={busy || waiting} onClick={() => void resend()}>
        {waiting ? `Gửi lại mã sau ${seconds}s` : 'Gửi lại mã'}
      </button>
    </> : <p>Nhập email của tài khoản đang chờ xác nhận để nhận mã.</p>}
    <nav className="auth-links"><a href="/resend-confirmation">{email ? 'Dùng email khác' : 'Nhập email'}</a><a href="/login">Về đăng nhập</a></nav>
  </section>;
}
