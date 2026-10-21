import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState,
  HttpTransportType, LogLevel } from '@microsoft/signalr';
import { getConversations, getMessages, getNotifications, markRead, reportMessage } from '../api/chatApi';
import type { ChatNotification, Conversation, Delivery, Message } from '../types/chat';

type Props = { token: string; userId: string; displayName: string; getAccessToken?: () => Promise<string> };
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
const mergeNotificationsById = (before: ChatNotification[], incoming: ChatNotification[]) => {
  const map = new Map(before.map(x => [x.id, x]));
  for (const item of incoming) map.set(item.id, item);
  return [...map.values()].sort((a, b) =>
    b.createdAt.localeCompare(a.createdAt) || b.id.localeCompare(a.id));
};

export function ChatPage({ token, userId, displayName, getAccessToken }: Props) {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [current, setCurrent] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [notifications, setNotifications] = useState<ChatNotification[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [notificationCursor, setNotificationCursor] = useState<string | null>(null);
  const [notificationBusy, setNotificationBusy] = useState(false);
  const [pending, setPending] = useState<Delivery[]>([]);
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState<ConnectionStatus>('Đang kết nối');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [feedback, setFeedback] = useState('');
  const connectionRef = useRef<HubConnection | null>(null);
  const currentRef = useRef(current);
  const messagesRef = useRef(messages);
  const notificationsRef = useRef(notifications);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => { currentRef.current = current; }, [current]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { notificationsRef.current = notifications; }, [notifications]);
  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages.length]);

  const refreshList = useCallback(async () => {
    const data = await getConversations(token);
    setConversations(data.items);
    setCurrent(prev => prev && data.items.some(x => x.id === prev)
      ? prev : data.items[0]?.id ?? null);
  }, [token]);
  const refreshNotifications = useCallback(async () => {
    const data = await getNotifications(token);
    setNotifications(prev => mergeNotificationsById(prev, data.items));
    setNotificationCursor(data.nextCursor);
  }, [token]);

  const catchUpNotifications = useCallback(async () => {
    const latest = notificationsRef.current[0];
    if (!latest) {
      await refreshNotifications();
      return;
    }
    let afterId: string | null = latest.id;
    for (let pageIndex = 0; afterId && pageIndex < 50; pageIndex++) {
      const page = await getNotifications(token, { afterId, pageSize: 50 });
      setNotifications(old => mergeNotificationsById(old, page.items));
      afterId = page.nextAfterId;
    }
  }, [token, refreshNotifications]);

  const catchUp = useCallback(async () => {
    const id = currentRef.current;
    if (!id) return;
    const latest = messagesRef.current.at(-1);
    if (!latest) {
      const page = await getMessages(token, id);
      if (currentRef.current !== id) return;
      setMessages(page.items);
      setPending(prev => prev.filter(p => !page.items.some(m => m.clientMessageId === p.clientMessageId)));
      setCursor(page.nextCursor);
      return;
    }
    let afterId: string | null = latest.id;
    for (let pageIndex = 0; afterId && pageIndex < 50; pageIndex++) {
      const page = await getMessages(token, id, { afterId });
      if (currentRef.current !== id) return;
      setMessages(old => mergeById(old, page.items));
      setPending(prev => prev.filter(p => !page.items.some(m => m.clientMessageId === p.clientMessageId)));
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
    let retryTimer: number | null = null;
    let startAttempt = 0;
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: () => getAccessToken ? getAccessToken() : token, transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = conn;

    const joinSelected = async () => {
      const selected = currentRef.current;
      if (selected) await conn.invoke('JoinConversation', selected);
    };
    const synchronize = () => {
      void catchUp().catch(e => setError(String(e)));
      void catchUpNotifications().catch(e => setError(String(e)));
      void refreshList().catch(() => {});
      void refreshNotifications().catch(() => {});
    };
    const requiresLogin = (cause: unknown) => {
      const text = cause instanceof Error ? cause.message : String(cause ?? '');
      return /\b(401|403)\b|unauthori[sz]ed|forbidden|token.*expired|phiên.*không hợp lệ/i.test(text);
    };
    const scheduleRetry = (cause: unknown) => {
      if (disposed || retryTimer !== null) return;
      if (requiresLogin(cause)) {
        setStatus('Mất kết nối');
        setError('Phiên đăng nhập không còn hợp lệ. Hãy đăng nhập lại.');
        return;
      }
      const delay = Math.min(30000, 1000 * (2 ** Math.min(startAttempt, 5)));
      startAttempt += 1;
      setStatus('Đang nối lại');
      retryTimer = window.setTimeout(() => {
        retryTimer = null;
        void startConnection();
      }, delay);
    };
    const startConnection = async () => {
      if (disposed || conn.state !== HubConnectionState.Disconnected) return;
      try {
        await conn.start();
      } catch (e) {
        if (!disposed) scheduleRetry(e);
        return;
      }
      if (disposed) { await conn.stop(); return; }
      startAttempt = 0;
      setStatus('Đã kết nối');
      setError('');
      try { await joinSelected(); } catch (e) { setError(String(e)); }
      synchronize();
    };

    conn.on('MessageCreated', (item: Message) => {
      if (item.conversationId === currentRef.current) {
        setMessages(prev => mergeById(prev, [item]));
        setPending(prev => prev.filter(p => p.clientMessageId !== item.clientMessageId));
      }
      void refreshList().catch(() => {});
    });
    conn.on('MatchCreated', () => {
      void refreshList().catch(() => {});
      void catchUpNotifications().catch(() => {});
      void refreshNotifications().catch(() => {});
    });
    conn.on('MatchEnded', () => {
      // Keep the conversation/history available, but refresh canSend from the current Match row.
      void refreshList().catch(e => setError(String(e)));
    });
    conn.on('NotificationCreated', (item: ChatNotification) => {
      setNotifications(prev => mergeNotificationsById(prev, [item]));
    });
    conn.onreconnecting(() => { if (!disposed) setStatus('Đang nối lại'); });
    conn.onreconnected(() => {
      if (disposed) return;
      setStatus('Đã kết nối');
      setError('');
      void joinSelected().catch(e => setError(String(e)));
      synchronize();
    });
    conn.onclose(error => {
      if (disposed) return;
      setStatus('Mất kết nối');
      scheduleRetry(error ?? new Error('SignalR connection closed.'));
    });
    void startConnection();
    return () => {
      disposed = true;
      if (retryTimer !== null) window.clearTimeout(retryTimer);
      connectionRef.current = null;
      void conn.stop();
    };
  }, [token, getAccessToken, refreshList, refreshNotifications, catchUp, catchUpNotifications]);

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
  const loadOldNotifications = async () => {
    if (!notificationCursor) return;
    try {
      setNotificationBusy(true);
      const page = await getNotifications(token, { cursor: notificationCursor });
      setNotifications(prev => mergeNotificationsById(prev, page.items));
      setNotificationCursor(page.nextCursor);
    } catch (e) { setError(String(e)); } finally { setNotificationBusy(false); }
  };
  const report = async (item: Message) => {
    const reason = window.prompt('Lý do báo cáo tin nhắn:');
    if (!reason?.trim()) return;
    const description = window.prompt('Mô tả thêm (ít nhất 10 ký tự):');
    if (!description || description.trim().length < 10) {
      setError('Mô tả báo cáo phải có ít nhất 10 ký tự.');
      return;
    }
    try {
      await reportMessage(token, item.id, reason.trim(), description.trim());
      setFeedback(`Đã gửi báo cáo cho tin nhắn ${item.id}.`);
      setError('');
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e);
      if (/HTTP 404|HTTP 501/.test(message)) {
        setError(`Yêu cầu báo cáo đã gửi đúng messageId ${item.id}, nhưng API xử lý báo cáo Message của TV5 chưa được tích hợp (lỗi ${message.match(/HTTP (404|501)/)?.[1]}).`);
      } else setError(message);
    }
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
      {feedback && <div role="status" className="feedback"><span>{feedback}</span>
        <button onClick={() => setFeedback('')} aria-label="Đóng thông báo">✕</button></div>}
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
                <time dateTime={m.sentAt}>{dateTime(m.sentAt)}</time>
                {m.senderId !== userId && <button type="button" className="report-message"
                  onClick={() => void report(m)}>Báo cáo</button>}
              </div></div>)}
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
          {notificationCursor && <button className="older" onClick={() => void loadOldNotifications()} disabled={notificationBusy}>
            {notificationBusy ? 'Đang tải…' : 'Tải thông báo cũ'}
          </button>}
        </aside>
      </div>
    </main>
  );
}
