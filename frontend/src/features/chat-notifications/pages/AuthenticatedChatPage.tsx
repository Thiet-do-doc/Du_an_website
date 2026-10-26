import { useCallback, useEffect, useRef } from 'react';
import { ProtectedRoute, useAuth } from '../../auth/AuthProvider';
import { refresh, type Session } from '../../auth/api';
import { ChatPage } from '../components/ChatPage';
import '../chat.css';

function SignedInChat() {
  const { session } = useAuth();
  const sessionRef = useRef<Session | null>(session);
  useEffect(() => { sessionRef.current = session; }, [session]);

  const getAccessToken = useCallback(async (): Promise<string> => {
    const current = sessionRef.current;
    if (!current) throw new Error('Phiên đăng nhập đã hết. Hãy đăng nhập lại.');
    // AuthProvider refreshes ahead of expiry; this extra guard covers a Hub handshake after a long sleep.
    if (Date.parse(current.expiresAt) - Date.now() > 60_000) return current.token;
    const fresh = await refresh();
    if (!fresh) {
      window.location.assign('/login');
      throw new Error('Phiên đăng nhập không còn hợp lệ. Hãy đăng nhập lại.');
    }
    sessionRef.current = fresh;
    return fresh.token;
  }, []);

  if (!session) return null;
  return <ChatPage token={session.token} userId={session.user.id}
    displayName="Tài khoản của bạn" getAccessToken={getAccessToken} />;
}

// Authenticated /chat route. The demo A/B/C selector is deliberately not routed through the real UI.
export default function AuthenticatedChatPage() {
  return <div className="tv4-demo"><ProtectedRoute><SignedInChat /></ProtectedRoute></div>;
}
