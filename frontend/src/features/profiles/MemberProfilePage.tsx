import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../auth/api';
import { likeProfile } from '../discovery-matching/api/discovery';
import { getPublicProfile } from './api';
import { PrivatePhoto } from './PrivatePhoto';
import type { PublicProfile } from './types';
import './profiles.css';

export default function MemberProfilePage({ userId }: { userId: string }) {
  const [profile, setProfile] = useState<PublicProfile | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [needsProfile, setNeedsProfile] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const reload = useCallback(() => setAttempt(n => n + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    setError('');
    getPublicProfile(userId, controller.signal)
      .then(p => { if (!controller.signal.aborted) setProfile(p); })
      .catch(e => {
        if (controller.signal.aborted) return;
        setProfile(null);
        setError(e instanceof ApiError && e.status === 404
          ? 'Hồ sơ hiện không thể xem.' : 'Chưa tải được hồ sơ. Hãy thử lại.');
      });
    return () => controller.abort();
  }, [userId, attempt]);

  const state = profile?.connection.state;
  useEffect(() => {
    if (state !== 'sent') return;
    window.addEventListener('focus', reload);
    return () => window.removeEventListener('focus', reload);
  }, [state, reload]);

  async function sendLike() {
    if (busy) return;
    setBusy(true); setError(''); setNeedsProfile(false);
    try {
      const result = await likeProfile(userId);
      setProfile(p => p && ({ ...p, connection: {
        state: result.isMatch ? 'matched' : 'sent', conversationId: result.conversationId
      } }));
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) { setProfile(null); setError('Hồ sơ hiện không thể kết nối.'); }
      else {
        setError(e instanceof Error ? e.message : 'Chưa gửi được lời thích.');
        setNeedsProfile(e instanceof ApiError && e.problem.title === 'Hồ sơ chưa hoàn thiện');
      }
    } finally { setBusy(false); }
  }

  const avatar = profile?.photos.find(p => p.isAvatar);
  const gallery = profile?.photos.filter(p => p.id !== avatar?.id) ?? [];
  return <section className="profile-page member-profile">
    <a className="text-link" href="/">← Bảng tin</a>
    {error && <p className="profile-alert" role="alert">{error} {needsProfile
      ? <a href="/profile">Hoàn thiện hồ sơ của bạn</a>
      : <button type="button" onClick={reload}>Thử lại</button>}</p>}
    {!profile ? !error && <p role="status">Đang tải hồ sơ…</p> :
      <div className="ml-card">
        <header className="member-profile-head">
          {avatar ? <PrivatePhoto className="member-portrait" src={avatar.url} alt={'Ảnh đại diện của ' + profile.displayName} />
            : <span className="member-portrait initials" aria-hidden="true">{profile.displayName.slice(0, 1)}</span>}
          <div><p className="ml-kicker">HỒ SƠ THÀNH VIÊN</p><h1>{profile.displayName}</h1>
            <p>{[profile.age + ' tuổi', profile.city, profile.gender].filter(Boolean).join(' · ')}</p></div>
        </header>
        {profile.bio && <p className="member-bio">{profile.bio}</p>}
        {profile.interests.length > 0 && <section><h2>Sở thích</h2><ul className="member-interests">
          {profile.interests.map(i => <li key={i.id}>{i.name}</li>)}</ul></section>}
        <div className="member-connect" aria-live="polite">
          {state === 'matched' && profile.connection.conversationId ? <>
            <p>Hai bạn đã ghép đôi.</p>
            <a className="btn btn-dark" href={'/chat?conversation=' + encodeURIComponent(profile.connection.conversationId)}>Nhắn tin</a>
          </> : state === 'sent' ? <><p>Đã gửi lời thích. Khi người ấy thích lại, hai bạn có thể nhắn tin.</p>
              <button type="button" className="btn btn-outline-dark" onClick={reload}>Cập nhật kết nối</button></>
            : state === 'unmatched' ? <p>Hai bạn đã bỏ ghép đôi.</p>
            : state === 'needsProfile' ? <><p>Lưu tên hiển thị, chọn ít nhất một sở thích và bật hiển thị trong Khám phá để người ấy có thể xem hồ sơ và thích lại.</p>
              <a className="btn btn-outline-dark" href="/profile">Hoàn thiện hồ sơ của bạn</a></>
            : !profile.interests.length ? <p>Hồ sơ này chưa hoàn thiện để nhận lời thích.</p>
            : <><p>{state === 'received' ? 'Người này đã gửi lời thích cho bạn.' : 'Khi cả hai cùng thích nhau, cuộc trò chuyện sẽ mở.'}</p>
              <button type="button" className="btn btn-dark" disabled={busy} onClick={() => void sendLike()}>
                {busy ? 'Đang gửi…' : state === 'received' ? 'Thích lại để ghép đôi' : 'Gửi lời thích'}</button></>}
        </div>
        {gallery.length > 0 && <section><h2>Ảnh hồ sơ</h2><div className="photo-grid">
          {gallery.map((p, i) => <div className="photo-item" key={p.id}><PrivatePhoto src={p.url} alt={'Ảnh hồ sơ ' + (i + 1)} /></div>)}
        </div></section>}
      </div>}
  </section>;
}
