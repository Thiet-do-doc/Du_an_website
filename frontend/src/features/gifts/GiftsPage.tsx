import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useAuth } from '../auth/AuthProvider';
import { authenticatedFetch, refresh } from '../auth/api';
import { getMatches } from '../discovery-matching/api/discovery';
import type { MatchSummary } from '../discovery-matching/types/discovery';
import type { Message } from '../chat-notifications/types/chat';
import { GiftArt } from './GiftArt';
import './gifts.css';

type Gift = { code: string; name: string; description: string };

export default function GiftsPage() {
  const { session } = useAuth();
  const [catalog, setCatalog] = useState<Gift[]>([]);
  const [matches, setMatches] = useState<MatchSummary[]>([]);
  const [recipient, setRecipient] = useState('');
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [historyVersion, setHistoryVersion] = useState(0);
  const pending = useRef<{ recipient: string; code: string; id: string } | null>(null);
  const sending = useRef(false);
  const selected = catalog.find(gift => gift.code === code);
  const match = matches.find(item => item.conversationId === recipient);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setLoadError(false);
    Promise.all([authenticatedFetch<Gift[]>('/api/v1/gifts'), getMatches()]).then(([gifts, people]) => {
      if (!active) return;
      setCatalog(gifts);
      setCode(gifts[0]?.code ?? '');
      setMatches(people);
      if (people.length === 1) setRecipient(people[0].conversationId);
    }).catch(() => { if (active) setLoadError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reload]);

  async function send() {
    if (!match || !selected || !session || sending.current) return;
    sending.current = true;
    setBusy(true); setNotice(''); setError('');
    if (!pending.current || pending.current.recipient !== recipient || pending.current.code !== code)
      pending.current = { recipient, code, id: crypto.randomUUID() };
    const connection = new HubConnectionBuilder().withUrl('/hubs/chat', {
      accessTokenFactory: async () => Date.parse(session.expiresAt) > Date.now() + 30_000
        ? session.token : (await refresh())?.token ?? '',
    }).build();
    try {
      await connection.start();
      await connection.invoke<Message>('SendGift', recipient, code, pending.current.id);
      pending.current = null;
      setNotice('Đã tặng “' + selected.name + '” cho ' + match.otherProfile.name + '.');
      setHistoryVersion(value => value + 1);
    } catch {
      setError('Chưa xác nhận được việc gửi quà. Kiểm tra kết nối và trạng thái ghép đôi rồi thử lại.');
    } finally {
      await connection.stop().catch(() => undefined);
      sending.current = false;
      setBusy(false);
    }
  }

  return <section className="feature-page gifts-page">
    <header><p className="ml-kicker">BƯU ĐIỆN MYLOVE</p><h1>Gửi một món quà,<br/><em>thêm một nụ cười.</em></h1>
      <p className="page-lead">Chọn một món quà ảo miễn phí cho người bạn đã ghép đôi.</p></header>
    {loading ? <p role="status">Đang mở bộ sưu tập…</p> : loadError ?
      <div role="alert">Chưa tải được góc quà tặng. <button className="btn btn-outline-dark" onClick={() => setReload(x => x + 1)}>Thử lại</button></div> : <>
      <fieldset disabled={busy}><legend className="visually-hidden">Chọn món quà</legend>
        <div className="gift-grid">{catalog.map((gift, i) => <label className={'gift-option theme-' + gift.code} key={gift.code}>
          <input type="radio" name="gift" checked={gift.code === code} onChange={() => { setCode(gift.code); setNotice(''); setError(''); }}/>
          <span className="gift-stamp"><small>MYLOVE POST</small><b>0{i + 1}</b><GiftArt code={gift.code}/></span>
          <strong>{gift.name}</strong><span>{gift.description}</span>
        </label>)}</div>
      </fieldset>
      {matches.length ? <form className="gift-dispatch ml-card" onSubmit={e => { e.preventDefault(); void send(); }}>
        <label>Người nhận<select required value={recipient} disabled={busy} onChange={e => { setRecipient(e.target.value); setNotice(''); setError(''); }}>
          <option value="">Chọn người đã ghép đôi</option>{matches.map(person => <option key={person.id} value={person.conversationId}>{person.otherProfile.name}</option>)}
        </select></label>
        <button className="btn btn-dark" disabled={busy || !match || !selected}>{busy ? 'Đang gửi…' : 'Gửi quà'}</button>
      </form> : <div className="ml-card"><h2>Bạn muốn tặng ai?</h2><p>Khi hai bạn thích nhau, tên người ấy sẽ xuất hiện ở đây.</p>
        <a className="text-link" href="/discover">Gặp người mới ↗</a></div>}
      {notice && <p className="auth-success" role="status">{notice} <a href={'/chat?conversation=' + recipient}>Mở hội thoại ↗</a></p>}
      {error && <p className="feed-error" role="alert">{error}</p>}
      {match && session && <GiftHistory key={recipient} recipient={recipient} userId={session.user.id} version={historyVersion}/>}
    </>}
  </section>;
}

function GiftHistory({ recipient, userId, version }: { recipient: string; userId: string; version: number }) {
  const [items, setItems] = useState<Message[] | null>(null);
  const [error, setError] = useState(false);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setError(false);
    authenticatedFetch<Message[]>('/api/v1/conversations/' + recipient + '/gifts', { signal: controller.signal })
      .then(value => { if (!controller.signal.aborted) setItems(value); })
      .catch(() => { if (!controller.signal.aborted) { setItems(null); setError(true); } });
    return () => controller.abort();
  }, [recipient, version, reload]);
  return <section className="gift-history ml-card"><header><h2>Quà gần đây của hai bạn</h2>
    <button className="btn btn-outline-dark" onClick={() => setReload(x => x + 1)}>Cập nhật</button></header>
    {error ? <p role="alert">Chưa đọc được lịch sử quà.</p> : !items ? <p role="status">Đang tải quà…</p> : items.length ?
      <ul>{items.map(item => <li key={item.id}><GiftArt code={item.giftCode!}/><div><small>{item.senderId === userId ? 'Bạn đã tặng' : 'Bạn đã nhận'}</small>
        <strong>{item.content}</strong></div><time dateTime={item.sentAt}>{new Date(item.sentAt).toLocaleString('vi-VN')}</time></li>)}</ul>
      : <p>Món quà đầu tiên của hai bạn sẽ xuất hiện ở đây.</p>}
    <a className="text-link" href={'/chat?conversation=' + recipient}>Xem hội thoại ↗</a>
  </section>;
}
