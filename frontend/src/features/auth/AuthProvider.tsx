import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { login, logout, publish, refresh, subscribe, type Session } from './api';
type AuthContextValue = { session: Session | null; ready: boolean; error: string; login: typeof login; logout: typeof logout; retry: () => void };
const AuthContext = createContext<AuthContextValue | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState('');
  async function restore() {
    setError('');
    try { setSession(await refresh()); } catch { setError('Chưa kết nối được máy chủ. Bạn có thể thử lại.'); }
    finally { setReady(true); }
  }
  useEffect(() => {
    const unsubscribe = subscribe(setSession);
    void restore();
    const channel = new BroadcastChannel('datingapp-auth');
    channel.onmessage = event => { if (event.data === 'logout') publish(null); };
    return () => { unsubscribe(); channel.close(); };
  }, []);
  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => { void refresh().catch(() => setError('Không thể gia hạn phiên. Hãy đăng nhập lại.')); },
      Math.max(1000, new Date(session.expiresAt).getTime() - Date.now() - 30000));
    return () => window.clearTimeout(timer);
  }, [session]);
  return <AuthContext.Provider value={{ session, ready, error, login, logout, retry: () => { void restore(); } }}>{children}</AuthContext.Provider>;
}
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('AuthProvider chưa được thiết lập.');
  return context;
}
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const auth = useAuth();
  if (!auth.ready) return <p role="status">Đang khôi phục phiên…</p>;
  if (!auth.session) return <div><p>Bạn cần đăng nhập để mở trang này.</p><a href="/login">Đăng nhập</a>
    {auth.error && <><p role="alert">{auth.error}</p><button onClick={auth.retry}>Thử kết nối lại</button></>}</div>;
  return <>{children}</>;
}
