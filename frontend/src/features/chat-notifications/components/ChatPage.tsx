import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, authenticatedFetch } from '../../auth/api';
import { blockUser } from '../../safety-admin/api/blocks';
import '../../gifts/gifts.css';
import { GiftArt } from '../../gifts/GiftArt';
import { HubConnection, HubConnectionBuilder, HubConnectionState,
  HttpTransportType, LogLevel } from '@microsoft/signalr';
import { getConversations, getMessages, reportMessage } from '../api/chatApi';
import type { Conversation, Delivery, Message } from '../types/chat';
import { NavIcon } from '../../../app/NavIcon';
import { useConversationAvatars } from './useConversationAvatars';

type Props = { token: string; userId: string; getAccessToken?: () => Promise<string> };
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
function ChatAvatar({ name, source }: { name: string; source?: string }) {
  return <span className="avatar" aria-hidden="true">
    {source ? <img src={source} alt="" /> : name.charAt(0).toLocaleUpperCase('vi-VN')}
  </span>;
}

export function ChatPage({ token, userId, getAccessToken }: Props) {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [current, setCurrent] = useState<string | null>(() => new URLSearchParams(location.search).get('conversation'));
  const [threadOpen, setThreadOpen] = useState(() => current !== null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [pending, setPending] = useState<Delivery[]>([]);
  const [giftOpen, setGiftOpen] = useState(false);
  const [gifts, setGifts] = useState<{ code: string; name: string; description: string }[]>([]);
  const [giftBusy, setGiftBusy] = useState(false);
  const [giftLoading, setGiftLoading] = useState(false);
  const [giftError, setGiftError] = useState('');
  const giftSending = useRef(false);
  const giftPending = useRef<{ conversation: string; code: string; id: string } | null>(null);
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState<ConnectionStatus>('Đang kết nối');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [feedback, setFeedback] = useState('');
  const connectionRef = useRef<HubConnection | null>(null);
  const currentRef = useRef(current);
  const messagesRef = useRef(messages);
  const refreshVersion = useRef(0);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const active = conversations.find(x => x.id === current);
  const blockedByPartner = Boolean(active?.blockedByPartner);
  const avatars = useConversationAvatars(conversations, userId);
  const activeAvatar = active && !blockedByPartner ? avatars?.get(active.partnerId) : undefined;
  const blockedRef = useRef(blockedByPartner);
  useEffect(() => {
    currentRef.current = current;
    blockedRef.current = blockedByPartner;
  }, [current, blockedByPartner]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => {
    const pane = bottomRef.current?.parentElement;
    pane?.scrollTo({ top: pane.scrollHeight, behavior: 'smooth' });
  }, [messages.length]);

  const refreshList = useCallback(async () => {
    const version = ++refreshVersion.current;
    const requested = new URLSearchParams(location.search).get('conversation');
    const wanted = currentRef.current || requested;
    let page = 1;
    let data = await getConversations(token);
    const items = [...data.items];
    while (wanted && data.hasMore && !items.some(x => x.id === wanted)) {
      data = await getConversations(token, ++page);
      items.push(...data.items);
    }
    if (version !== refreshVersion.current) return;
    setConversations(items);
    setCurrent(prev => {
      const selected = prev || requested;
      return selected ? items.find(x => x.id === selected)?.id ?? null : items[0]?.id ?? null;
    });
    if (requested && wanted === requested && !items.some(x => x.id === requested))
      setError('Cuộc trò chuyện này hiện không thể mở.');
  }, [token]);
  const catchUp = useCallback(async () => {
    const id = currentRef.current;
    if (!id || blockedRef.current) return;
    const latest = messagesRef.current.at(-1);
    if (!latest) {
      const page = await getMessages(token, id);
      if (currentRef.current !== id || blockedRef.current) return;
      setMessages(page.items);
      setPending(prev => prev.filter(p => !page.items.some(m => m.clientMessageId === p.clientMessageId)));
      setCursor(page.nextCursor);
      return;
    }
    let afterId: string | null = latest.id;
    for (let pageIndex = 0; afterId && pageIndex < 50; pageIndex++) {
      const page = await getMessages(token, id, { afterId });
      if (currentRef.current !== id || blockedRef.current) return;
      setMessages(old => mergeById(old, page.items));
      setPending(prev => prev.filter(p => !page.items.some(m => m.clientMessageId === p.clientMessageId)));
      afterId = page.nextAfterId;
    }
  }, [token]);

  useEffect(() => {
    void refreshList().catch(e => setError(String(e)));
  }, [refreshList]);

  useEffect(() => {
    setMessages([]); setPending([]); setCursor(null);
    if (!current || blockedByPartner) {
      setBusy(false);
      return;
    }
    let cancelled = false;
    setBusy(true);
    void getMessages(token, current).then(page => {
      if (!cancelled) { setMessages(page.items); setCursor(page.nextCursor); }
    }).catch(e => {
      if (cancelled) return;
      if (e instanceof ApiError && e.status === 404) void refreshList().catch(() => {});
      else setError(String(e));
    })
      .finally(() => { if (!cancelled) setBusy(false); });
    // Rejoin on selection. A rejected join must NOT expose any messages.
    const con = connectionRef.current;
    if (con?.state === HubConnectionState.Connected) {
      void con.invoke('JoinConversation', current).catch(() => {
        if (!cancelled) void refreshList().catch(() => {});
      });
    }
    return () => { cancelled = true; };
  }, [current, token, blockedByPartner, refreshList]);

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
      if (selected && !blockedRef.current) await conn.invoke('JoinConversation', selected);
    };
    const synchronize = () => {
      void catchUp().catch(e => {
        if (!(e instanceof ApiError && e.status === 404)) setError(String(e));
      });
      void refreshList().catch(() => {});
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
      await joinSelected().catch(() => {});
      synchronize();
    };

    conn.on('MessageCreated', (item: Message) => {
      if (item.conversationId === currentRef.current && !blockedRef.current) {
        setMessages(prev => mergeById(prev, [item]));
        setPending(prev => prev.filter(p => p.clientMessageId !== item.clientMessageId));
      }
      void refreshList().catch(() => {});
    });
    conn.on('MatchCreated', () => {
      void refreshList().catch(() => {});
    });
    conn.on('MatchEnded', () => {
      // Keep the conversation/history available, but refresh canSend from the current Match row.
      void refreshList().catch(e => setError(String(e)));
    });
    const refreshAccess = () => { void refreshList().catch(() => {}); };
    conn.on('ConversationAccessChanged', refreshAccess);
    window.addEventListener('focus', refreshAccess);
    conn.onreconnecting(() => { if (!disposed) setStatus('Đang nối lại'); });
    conn.onreconnected(() => {
      if (disposed) return;
      setStatus('Đã kết nối');
      setError('');
      void joinSelected().catch(refreshAccess);
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
      window.removeEventListener('focus', refreshAccess);
      if (retryTimer !== null) window.clearTimeout(retryTimer);
      connectionRef.current = null;
      void conn.stop();
    };
  }, [token, getAccessToken, refreshList, catchUp]);

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
    if (!active?.canSend) return;
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
  useEffect(() => { setGiftOpen(false); setGiftError(''); }, [current, blockedByPartner]);
  async function openGifts() {
    if (giftOpen) { setGiftOpen(false); return; }
    setGiftOpen(true); setGiftError(''); setGiftLoading(true);
    try { setGifts(await authenticatedFetch('/api/v1/gifts')); }
    catch { setGiftError('Chưa tải được quà. Đóng rồi mở lại để thử lại.'); }
    finally { setGiftLoading(false); }
  }
  async function sendGift(code: string) {
    const conn = connectionRef.current;
    if (!current || !active?.canSend || giftSending.current || conn?.state !== HubConnectionState.Connected) return;
    giftSending.current = true; setGiftBusy(true); setGiftError('');
    const pending = giftPending.current;
    if (!pending || pending.conversation !== current || pending.code !== code)
      giftPending.current = { conversation: current, code, id: crypto.randomUUID() };
    const attempt = giftPending.current!;
    try {
      const saved = await conn.invoke<Message>('SendGift', attempt.conversation, attempt.code, attempt.id);
      if (currentRef.current === saved.conversationId) {
        setMessages(old => mergeById(old, [saved])); setGiftOpen(false);
      }
      giftPending.current = null;
      void refreshList().catch(() => {});
    } catch { setGiftError('Chưa xác nhận được việc gửi quà. Chọn lại món quà để thử lại, quà sẽ không bị gửi trùng.'); }
    finally { giftSending.current = false; setGiftBusy(false); }
  }
  const blockPartner = async (target: Conversation) => {
    if (!window.confirm(`Chặn ${target.partnerName}? Cuộc trò chuyện và kết nối với người này sẽ bị ẩn cho tới khi bạn bỏ chặn.`)) return;
    setError('');
    try {
      await blockUser(target.partnerId);
      setGiftOpen(false);
      setCurrent(null);
      setMessages([]);
      await refreshList();
      window.dispatchEvent(new Event('notifications-read'));
      setFeedback(`Đã chặn ${target.partnerName}. Bạn có thể bỏ chặn trong mục An toàn.`);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  };
  return (
    <section className="shell" aria-labelledby="chat-title">
      <header className="app-header">
        <div><h1 id="chat-title">Trò chuyện</h1><p>Tin nhắn với những người bạn đã ghép đôi.</p></div>
        <div className="connection" aria-live="polite"><span className={status === 'Đã kết nối' ? 'dot ok' : 'dot'} /> {status}</div>
      </header>
      {error && <div role="alert" className="alert"><span>{error}</span>
        <button onClick={() => setError('')} aria-label="Đóng lỗi">✕</button></div>}
      {feedback && <div role="status" className="feedback"><span>{feedback}</span>
        <button onClick={() => setFeedback('')} aria-label="Đóng thông báo">✕</button></div>}
      <div className={threadOpen && active ? 'layout thread-open' : 'layout'}>
        <aside className="conversations">
          <div className="section-heading"><h2>Hội thoại</h2><span>{conversations.length}</span></div>
          {conversations.length === 0 ? <p className="empty">Chưa có hội thoại. Match hai chiều sẽ tạo chat.</p>
            : conversations.map(c => (
              <button key={c.id} className={`person ${current === c.id ? 'selected' : ''}`}
                onClick={() => { setCurrent(c.id); setThreadOpen(true); }}>
                <ChatAvatar name={c.partnerName} source={c.blockedByPartner ? undefined : avatars?.get(c.partnerId)} />
                <span className="person-detail"><b>{c.partnerName}</b><small>{c.blockedByPartner ? 'Bạn đã bị chặn' : c.lastMessage?.content ?? 'Bắt đầu trò chuyện'}</small></span>
                {!c.canSend && !c.blockedByPartner && <span className="readonly">Chỉ đọc</span>}
              </button>
            ))}
        </aside>
        <section className="chat-pane" aria-label="Cửa sổ trò chuyện">
          <div className="chat-top">
            <button type="button" className="chat-back" onClick={() => setThreadOpen(false)} aria-label="Về danh sách hội thoại">←</button>
            <div className="chat-identity">
              {active && <ChatAvatar name={active.partnerName} source={activeAvatar} />}
              <div><h2>{active?.partnerName ?? 'Chọn cuộc trò chuyện'}</h2>
                {!blockedByPartner && <small>{active?.canSend ? 'Hội thoại đang hoạt động' : 'Chưa chọn hoặc không thể gửi'}</small>}</div>
            </div>
            {active && <div className="chat-top-actions">
              {!blockedByPartner && <a className="chat-action" href={'/profile?user=' + encodeURIComponent(active.partnerId)}>Hồ sơ</a>}
              <button type="button" className="chat-block" onClick={() => void blockPartner(active)}>Chặn</button>
            </div>}
          </div>
          {blockedByPartner ? <div className="messages">
            <div className="blocked-conversation" role="status">
              <NavIcon name="lock" />
              <strong>Bạn đã bị chặn</strong>
              <p>Bạn không thể gửi tin nhắn hoặc quà trong cuộc hội thoại này.</p>
            </div>
          </div> : <><div className="messages" aria-live="polite">
            {cursor && <button className="older" onClick={() => void loadOld()} disabled={busy}>Tải tin nhắn cũ</button>}
            {busy && <p className="muted">Đang tải...</p>}
            {messages.length === 0 && !busy && <div className="empty centered">Chưa có tin nhắn.</div>}
            {messages.map((m, index) => {
              const mine = m.senderId === userId;
              const showAvatar = active?.partnerId === m.senderId && messages[index - 1]?.senderId !== m.senderId;
              return <div key={m.id} className={`bubble-row ${mine ? 'mine' : 'incoming'}`}>
                {active && showAvatar && <ChatAvatar name={active.partnerName} source={activeAvatar} />}
                <div className="bubble">{m.giftCode && <><GiftArt code={m.giftCode}/><small>Quà tặng mylove</small></>}<div className="bubble-text">{m.content}</div>
                  <time dateTime={m.sentAt}>{dateTime(m.sentAt)}</time>
                  {!mine && <button type="button" className="report-message"
                    onClick={() => void report(m)}>Báo cáo</button>}
                </div>
              </div>;
            })}
            {pending.map(p => <div className="bubble-row mine" key={p.clientMessageId}>
              <div className="bubble pending"><div className="bubble-text">{p.content}</div>
                <small>{p.status === 'sending' ? 'Đang gửi...' : 'Gửi thất bại'}</small>
                {p.status === 'failed' && <button onClick={() => void retry(p)}>Thử lại</button>}
              </div></div>)}
            <div ref={bottomRef} />
          </div>
          {giftOpen && <section className="chat-gifts" aria-label="Chọn quà tặng">
            <header><strong>Tặng quà cho {active?.partnerName}</strong><button type="button" onClick={() => setGiftOpen(false)} aria-label="Đóng quà tặng">✕</button></header>
            <p>Quà ảo miễn phí · Chọn một món để gửi vào hội thoại.</p>
            {giftLoading && <p role="status">Đang tải quà…</p>}
            {giftError && <p role="alert">{giftError}</p>}
            <div>{gifts.map(gift => <button key={gift.code} type="button" disabled={giftBusy || !active?.canSend || status !== 'Đã kết nối'} onClick={() => void sendGift(gift.code)} title={gift.description}>
              <GiftArt code={gift.code}/><span>{gift.name}</span>
            </button>)}</div>{giftBusy && <p role="status">Đang gửi quà…</p>}
          </section>}
          <form className="composer" onSubmit={e => { e.preventDefault(); void send(); }}>
            <button type="button" className="gift-toggle" aria-label="Tặng quà" aria-expanded={giftOpen} disabled={!active?.canSend || giftBusy} onClick={() => void openGifts()}><NavIcon name="gift" /></button>
            <label htmlFor="message-input" className="sr-only">Nhập nội dung tin nhắn</label>
            <input id="message-input" value={draft} onChange={e => setDraft(e.target.value)}
              disabled={!active?.canSend} maxLength={2000} placeholder="Nhập tin nhắn..." />
            <button disabled={!active?.canSend || !draft.trim() || status !== 'Đã kết nối'} type="submit">Gửi</button>
          </form></>}
        </section>
      </div>
    </section>
  );
}
