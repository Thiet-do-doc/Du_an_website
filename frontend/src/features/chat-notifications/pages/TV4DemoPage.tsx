import { useState } from 'react';
import { ChatPage } from '../components/ChatPage';

type Session = { token: string; userId: string; displayName: string; alias: string };

// Demonstration login ONLY. TV1 replaces this selector with AuthProvider + JWT.
export default function TV4DemoPage() {
  const [session, setSession] = useState<Session | null>(null);
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const login = async (alias: string) => {
    setLoading(true); setMessage('');
    try {
      const response = await fetch(`/api/v1/dev/token/${alias}`);
      if (!response.ok) throw new Error(`HTTP ${response.status}. Backend phải chạy trong môi trường Development.`);
      const data = await response.json() as Omit<Session, 'alias'>;
      setSession({ ...data, alias });
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setLoading(false); }
  };
  return <>
    <div className="demo-strip">
      <span><b>TV4 ChatLab</b> · Bản minh họa chạy local, tài khoản giả A/B/C</span>
      <nav aria-label="Chọn tài khoản giả">
        {['A', 'B', 'C'].map(alias => <button key={alias}
          className={session?.alias === alias ? 'chosen' : ''} onClick={() => void login(alias)}
          disabled={loading}>Tài khoản {alias}</button>)}
      </nav>
    </div>
    {message && <p className="alert" role="alert">{message}</p>}
    {!session ? <div className="welcome"><h1>Demo Chat · Thành viên 4</h1>
      <p>Chọn tài khoản A hoặc B để vào đoạn hội thoại đã ghép đôi.</p>
      <p>Mở thêm một cửa sổ ẩn danh để thử đồng thời hai tài khoản.</p>
      <p>Tài khoản C chưa match với A/B và không được đọc hội thoại này.</p></div>
      : <ChatPage key={session.alias} token={session.token}
        userId={session.userId} displayName={session.displayName} />}
  </>;
}
