import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState,
  HttpTransportType, LogLevel } from '@microsoft/signalr';
import { getConversations, getMessages, getNotifications, markRead } from '../api/chatApi';
import type { ChatNotification, Conversation, Delivery, Message } from '../types/chat';

type Props = { token: string; userId: string; displayName: string };
type ConnectionStatus = 'Đang kết nối' | 'Đã kết nối' | 'Đang nối lại' | 'Mất kết nối';

const dateTime = (iso: string) => new Date(iso).toLocaleString('vi-VN', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
});
const mergeById = (before: Message[], incoming: Message[]) => {
  const map = new Map(before.map(x => [x.id, x]));
  for (const item of incoming) map.set(item.id, item);
  return [...map.values()].sort((a, b) =>
    a.sentAt.localeCompare(b.sentAt) || a.id.localeCompare(b.id));
};

export function ChatPage({ token, userId, displayName }: Props) {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [current, setCurrent] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [notifications, setNotifications] = useState<ChatNotification[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [pending, setPending] = useState<Delivery[]>([]);
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState<ConnectionStatus>('Đang kết nối');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const connectionRef = useRef<HubConnection | null>(null);
  const currentRef = useRef(current);
  const messagesRef = useRef(messages);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => { currentRef.current = current; }, [current]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages.length]);

  const refreshList = useCallback(async () => {
    const data = await getConversations(token);
    setConversations(data.items);
    setCurrent(prev => prev && data.items.some(x => x.id === prev)
      ? prev : data.items[0]?.id ?? null);
  }, [token]);
  const refreshNotifications = useCallback(async () => {
    const data = await getNotifications(token);
    setNotifications(data.items);
  }, [token]);

  const catchUp = useCallback(async () => {
    const id = currentRef.current;
    if (!id) return;
    const latest = messagesRef.current.at(-1);
    if (!latest) {
      const page = await getMessages(token, id);
      if (currentRef.current !== id) return;
      setMessages(page.items);
      setCursor(page.nextCursor);
      return;
    }
    let afterId: string | null = latest.id;
    for (let pageIndex = 0; afterId && pageIndex < 50; pageIndex++) {
      const page = await getMessages(token, id, { afterId });
      if (currentRef.current !== id) return;
      setMessages(old => mergeById(old, page.items));
      afterId = page.nextAfterId;
    }
  }, [token]);

  useEffect(() => {
    void refreshList().catch(e => setError(String(e)));
    void refreshNotifications().catch(e => setError(String(e)));
  }, [refreshList, refreshNotifications]);

  useEffect(() => {
    if (!current) {
      setMessages([]); setCursor(null); return;
    }
    let cancelled = false;
    setMessages([]); setPending([]); setCursor(null); setBusy(true);
    void getMessages(token, current).then(page => {
      if (!cancelled) { setMessages(page.items); setCursor(page.nextCursor); }
    }).catch(e => { if (!cancelled) setError(String(e)); })
      .finally(() => { if (!cancelled) setBusy(false); });
    // Rejoin on selection. A rejected join must NOT expose any messages.
    const con = connectionRef.current;
    if (con?.state === HubConnectionState.Connected) {
      void con.invoke('JoinConversation', current).catch(e => setError(String(e)));
    }
    return () => { cancelled = true; };
  }, [current, token]);

  useEffect(() => {
    let disposed = false;
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: () => token, transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = conn;
    conn.on('MessageCreated', (item: Message) => {
      if (item.conversationId === currentRef.current) {
        setMessages(prev => mergeById(prev, [item]));
        setPending(prev => prev.filter(p => p.clientMessageId !== item.clientMessageId));
      }
      void refreshList().catch(() => {});
    });
    conn.on('MatchCreated', () => {
      void refreshList().catch(() => {});
      void refreshNotifications().catch(() => {});
    });
    conn.on('NotificationCreated', (item: ChatNotification) => {
      setNotifications(prev => [item, ...prev.filter(x => x.id !== item.id)]);
    });
    conn.onreconnecting(() => { if (!disposed) setStatus('Đang nối lại'); });
    conn.onreconnected(() => {
      if (disposed) return;
      setStatus('Đã kết nối');
      const selected = currentRef.current;
      if (selected) void conn.invoke('JoinConversation', selected).catch(e => setError(String(e)));
      void catchUp().catch(e => setError(String(e)));
      void refreshList().catch(e => setError(String(e)));
      void refreshNotifications().catch(e => setError(String(e)));
    });
    conn.onclose(() => { if (!disposed) setStatus('Mất kết nối'); });
    void conn.start().then(async () => {
      if (disposed) return;
      setStatus('Đã kết nối');
      if (currentRef.current) await conn.invoke('JoinConversation', currentRef.current);
      await catchUp();
    }).catch(e => { if (!disposed) { setStatus('Mất kết nối'); setError(String(e)); } });
    return () => {
      disposed = true;
      connectionRef.current = null;
      void conn.stop();
    };
  }, [token, refreshList, refreshNotifications, catchUp]);

  const sendWithId = async (content: string, clientMessageId: string) => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected || !current)
      throw new Error('Chưa có kết nối tới máy chủ.');
    const saved = await conn.invoke<Message>('SendMessage', current, content, clientMessageId);
    if (currentRef.current === saved.conversationId)
      setMessages(prev => mergeById(prev, [saved]));
    setPending(prev => prev.filter(p => p.clientMessageId !== clientMessageId));
  };
  const send = async () => {
    const content = draft.trim();
    if (!content || content.length > 2000 || !active?.canSend) return;
    const id = crypto.randomUUID();
    setDraft('');
    setPending(prev => [...prev, { content, clientMessageId: id, status: 'sending' }]);
    try { await sendWithId(content, id); }
    catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setPending(prev => prev.map(p => p.clientMessageId === id ? { ...p, status: 'failed' as const } : p));
    }
  };
  const retry = async (item: Delivery) => {
    setPending(prev => prev.map(p => p.clientMessageId === item.clientMessageId ? { ...p, status: 'sending' as const } : p));
    try { await sendWithId(item.content, item.clientMessageId); }
    catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setPending(prev => prev.map(p => p.clientMessageId === item.clientMessageId ? { ...p, status: 'failed' as const } : p));
    }
  };
  const loadOld = async () => {
    if (!current || !cursor) return;
    try {
      setBusy(true);
      const page = await getMessages(token, current, { cursor });
      setMessages(prev => mergeById(page.items, prev));
      setCursor(page.nextCursor);
    } catch (e) { setError(String(e)); } finally { setBusy(false); }
  };
  const read = async (notificationId: string) => {
    try {
      const item = await markRead(token, notificationId);
      setNotifications(prev => prev.map(x => x.id === item.id ? item : x));
    } catch (e) { setError(String(e)); }
  };
  const active = conversations.find(x => x.id === current);
  const unread = notifications.filter(x => !x.readAt).length;

  return (
    <main className="shell">
      <header className="app-header">
        <div><p className="eyebrow">THÀNH VIÊN 4 · CHAT & NOTIFICATIONS</p>
          <h1>Trung tâm trò chuyện</h1><span>Đang xem với vai trò: <b>{displayName}</b></span></div>
        <div className="connection" aria-live="polite"><span className={status === 'Đã kết nối' ? 'dot ok' : 'dot'} /> {status}</div>
      </header>
      {error && <div role="alert" className="alert"><span>{error}</span>
        <button onClick={() => setError('')} aria-label="Đóng lỗi">✕</button></div>}
      <div className="layout">
        <aside className="conversations">
          <div className="section-heading"><h2>Hội thoại</h2><span>{conversations.length}</span></div>
          {conversations.length === 0 ? <p className="empty">Chưa có hội thoại. Match hai chiều sẽ tạo chat.</p>
            : conversations.map(c => (
              <button key={c.id} className={`person ${current === c.id ? 'selected' : ''}`}
                onClick={() => setCurrent(c.id)}>
                <span className="avatar">{c.partnerName.charAt(0).toUpperCase()}</span>
                <span className="person-detail"><b>{c.partnerName}</b><small>{c.lastMessage?.content ?? 'Bắt đầu trò chuyện'}</small></span>
                {!c.canSend && <span className="readonly">Chỉ đọc</span>}
              </button>
            ))}
        </aside>
        <section className="chat-pane" aria-label="Cửa sổ trò chuyện">
          <div className="chat-top"><h2>{active?.partnerName ?? 'Chọn cuộc trò chuyện'}</h2>
            <small>{active?.canSend ? 'Hội thoại đang hoạt động' : 'Chưa chọn hoặc không thể gửi'}</small></div>
          <div className="messages" aria-live="polite">
            {cursor && <button className="older" onClick={() => void loadOld()} disabled={busy}>Tải tin nhắn cũ</button>}
            {busy && <p className="muted">Đang tải...</p>}
            {messages.length === 0 && !busy && <div className="empty centered">Chưa có tin nhắn.</div>}
            {messages.map(m => <div key={m.id} className={`bubble-row ${m.senderId === userId ? 'mine' : ''}`}>
              <div className="bubble"><div className="bubble-text">{m.content}</div>
                <time dateTime={m.sentAt}>{dateTime(m.sentAt)}</time></div></div>)}
            {pending.map(p => <div className="bubble-row mine" key={p.clientMessageId}>
              <div className="bubble pending"><div className="bubble-text">{p.content}</div>
                <small>{p.status === 'sending' ? 'Đang gửi...' : 'Gửi thất bại'}</small>
                {p.status === 'failed' && <button onClick={() => void retry(p)}>Thử lại</button>}
              </div></div>)}
            <div ref={bottomRef} />
          </div>
          <form className="composer" onSubmit={e => { e.preventDefault(); void send(); }}>
            <label htmlFor="message-input" className="sr-only">Nhập nội dung tin nhắn</label>
            <input id="message-input" value={draft} onChange={e => setDraft(e.target.value)}
              disabled={!active?.canSend} maxLength={2000} placeholder="Nhập tin nhắn..." />
            <button disabled={!active?.canSend || !draft.trim() || status !== 'Đã kết nối'} type="submit">Gửi</button>
          </form>
        </section>
        <aside className="notifications">
          <div className="section-heading"><h2>Thông báo</h2><span>{unread} mới</span></div>
          {notifications.length === 0 ? <p className="empty">Chưa có thông báo.</p>
            : notifications.map(item => <div className={`notice ${item.readAt ? '' : 'unread'}`} key={item.id}>
              <b>{item.type === 'match' ? 'Ghép đôi mới' : 'Tin nhắn mới'}</b>
              <small>{dateTime(item.createdAt)}</small>
              {!item.readAt && <button onClick={() => void read(item.id)}>Đánh dấu đã đọc</button>}
            </div>)}
        </aside>
      </div>
    </main>
  );
}
