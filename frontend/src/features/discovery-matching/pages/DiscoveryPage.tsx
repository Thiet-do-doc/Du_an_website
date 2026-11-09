import { PhotoBackground } from '../../profiles/PrivatePhoto';
import { useEffect, useRef, useState } from 'react';
import { getDiscover, getMatches, getReceivedLikes, likeProfile, passProfile, unmatch, type ReceivedLike } from '../api/discovery';
import { getInterests } from '../../profiles/api';
import { blockUser } from '../../safety-admin/api/blocks';
import type { Interest } from '../../profiles/types';
import type { DiscoveryFilters, DiscoveryProfile, MatchSummary } from '../types/discovery';


const initialFilters: DiscoveryFilters = { minAge: 18, maxAge: 35, city: 'Tất cả khu vực', interest: 'Tất cả sở thích' };

function DotOrbit() {
  return <svg className="dot-orbit" viewBox="0 0 48 48" width="48" height="48" aria-hidden="true">
    {Array.from({ length: 10 }, (_, i) => <circle key={i} cx="24" cy="6" r={1.6 + i * .18}
      transform={'rotate(' + i * 36 + ' 24 24)'} fill="currentColor" opacity={.2 + i * .085} />)}
  </svg>;
}

export default function DiscoveryPage() {
  const [filters, setFilters] = useState(initialFilters);
  const [profiles, setProfiles] = useState<DiscoveryProfile[]>([]);
  const [matches, setMatches] = useState<MatchSummary[]>([]);
  const [received, setReceived] = useState<ReceivedLike[]>([]);
  const [connectionsLoading, setConnectionsLoading] = useState(true);
  const [connectionsError, setConnectionsError] = useState('');
  const [interests, setInterests] = useState<Interest[]>([]);
  const [catalogError, setCatalogError] = useState(false);
  const [catalogAttempt, setCatalogAttempt] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [page, setPage] = useState(1);
  const [index, setIndex] = useState(0);
  const [activeTab, setActiveTab] = useState<'discover' | 'matches'>('discover');
  const [loading, setLoading] = useState(true);
  const [acting, setActing] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    setCatalogError(false);
    getInterests().then(items => { if (active) setInterests(items); })
      .catch(() => { if (active) setCatalogError(true); });
    return () => { active = false; };
  }, [catalogAttempt]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    getDiscover(filters, page, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      setProfiles(current => page === 1 ? result.items : [...current, ...result.items]);
      setHasMore(result.hasMore);
      if (page === 1) setIndex(0);
    }).catch(reason => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Không tải được hồ sơ.');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filters, page]);

  useEffect(() => {
    let current = true;
    setConnectionsLoading(true); setConnectionsError('');
    Promise.all([getMatches(), getReceivedLikes()]).then(([connections, likes]) => {
      if (current) { setMatches(connections); setReceived(likes); }
    })
      .catch(() => { if (current) setConnectionsError('Chưa tải được lời thích và kết nối.'); })
      .finally(() => { if (current) setConnectionsLoading(false); });
    return () => { current = false; };
  }, [activeTab]);

  const profile = profiles[index];
  const connectionCount = connectionsLoading || connectionsError ? null : received.length + matches.length;
  const today = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());

  useEffect(() => { setMenuOpen(false); }, [profile?.id]);
  useEffect(() => {
    if (!menuOpen) return;
    const closeOutside = (event: PointerEvent) => { if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false); };
    const closeEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenuOpen(false); };
    document.addEventListener('pointerdown', closeOutside);
    document.addEventListener('keydown', closeEscape);
    return () => { document.removeEventListener('pointerdown', closeOutside); document.removeEventListener('keydown', closeEscape); };
  }, [menuOpen]);

  async function advance(action: 'like' | 'pass') {
    if (!profile || acting) return;
    setActing(true);
    setMenuOpen(false);
    setError('');
    try {
      if (action === 'like') {
        const result = await likeProfile(profile.id);
        setNotice(result.isMatch ? `Hai bạn đã ghép đôi với ${profile.name}.` : `Đã gửi lượt thích cho ${profile.name}.`);
      } else {
        await passProfile(profile.id);
        setNotice('Đã bỏ qua hồ sơ này.');
      }
      if (index + 1 < profiles.length) setIndex(index + 1);
      else if (hasMore) setPage(current => current + 1);
      else setIndex(profiles.length);
      if (action === 'like') getMatches().then(setMatches).catch(() => undefined);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không hoàn tất được thao tác.');
    } finally { setActing(false); }
  }

  async function blockProfileCard(target: DiscoveryProfile) {
    if (acting) return;
    setMenuOpen(false);
    if (!window.confirm(`Chặn ${target.name}? Hai bạn sẽ không thấy hồ sơ, bài viết hay nhắn tin cho nhau nữa.`)) return;
    setActing(true);
    setError('');
    try {
      await blockUser(target.id);
      setNotice(`Đã chặn ${target.name}. Bạn có thể bỏ chặn trong mục An toàn.`);
      window.dispatchEvent(new Event('notifications-read'));
      if (index + 1 < profiles.length) setIndex(index + 1);
      else if (hasMore) setPage(current => current + 1);
      else setIndex(profiles.length);
      Promise.all([getMatches(), getReceivedLikes()]).then(([connections, likes]) => {
        setMatches(connections);
        setReceived(likes);
      }).catch(() => undefined);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không chặn được hồ sơ.');
    } finally { setActing(false); }
  }

  function updateFilter<K extends keyof DiscoveryFilters>(key: K, value: DiscoveryFilters[K]) {
    setFilters(current => ({ ...current, [key]: value }));
    setPage(1);
    setIndex(0);
    setProfiles([]);
  }

  async function removeMatch(id: string) {
    try {
      await unmatch(id);
      setMatches(current => current.filter(match => match.id !== id));
      setNotice('Đã bỏ ghép đôi. Quyền gửi tin mới đã bị thu hồi.');
    } catch (reason) { setConnectionsError(reason instanceof Error ? reason.message : 'Không thể bỏ ghép đôi.'); }
  }

  return <div className="dating-shell">
    <header className="dating-topbar">
      <nav className="dating-nav" aria-label="Điều hướng chính">
        <button className={activeTab === 'discover' ? 'nav-item selected' : 'nav-item'} onClick={() => { setError(''); setActiveTab('discover'); }}>Khám phá</button>
        <button className={activeTab === 'matches' ? 'nav-item selected' : 'nav-item'} onClick={() => { setError(''); setActiveTab('matches'); }}>Kết nối {connectionCount !== null && <span className="nav-count">{connectionCount}</span>}</button>
      </nav>

    </header>

    <main className="discovery-layout">
      <aside className="discovery-sidebar">
        <p className="sidebar-eyebrow">KHÔNG GIAN CỦA BẠN</p>
        <button className={activeTab === 'discover' ? 'side-link active' : 'side-link'} onClick={() => setActiveTab('discover')}><span>✳</span> Khám phá</button>
        <button className={activeTab === 'matches' ? 'side-link active' : 'side-link'} onClick={() => setActiveTab('matches')}><span>♡</span> Lượt thích & kết nối {connectionCount !== null && <b>{connectionCount}</b>}</button>
        <div className="sidebar-note"><span className="note-spark">✳</span><p>Một kết nối tốt bắt đầu bằng sự tò mò chân thành.</p></div>
        <p className="sidebar-foot">Mỗi lời chào là một khởi đầu.</p>
      </aside>

      {activeTab === 'discover' ? <section className="discover-main">
        <div className="discover-heading"><div><p className="sidebar-eyebrow">{today}</p><h1>Gặp gỡ <em>người mới.</em></h1><p>Những người có thể cùng bạn tạo nên một câu chuyện hay.</p></div>
          <div className="online-pill"><span /> Khám phá hồ sơ</div></div>
        <div className="filter-row" aria-label="Bộ lọc khám phá">
          <label>Khu vực<select value={filters.city} onChange={e => updateFilter('city', e.target.value)}><option>Tất cả khu vực</option><option>TP. Hồ Chí Minh</option><option>Thủ Đức</option><option>Bình Thạnh</option></select></label>
          <label>Độ tuổi<select value={`${filters.minAge}-${filters.maxAge}`} onChange={e => { const [minAge, maxAge] = e.target.value.split('-').map(Number); setFilters(f => ({ ...f, minAge, maxAge })); setPage(1); setIndex(0); setProfiles([]); }}><option value="18-35">18 – 35</option><option value="18-25">18 – 25</option><option value="23-30">23 – 30</option><option value="25-35">25 – 35</option></select></label>
          <label>Sở thích<select aria-label="Sở thích" value={filters.interest} onChange={e => updateFilter('interest', e.target.value)}><option>Tất cả sở thích</option>{interests.map(item => <option key={item.id} value={item.name}>{item.name}</option>)}</select></label>
          {catalogError && <button type="button" onClick={() => setCatalogAttempt(n => n + 1)}>Tải lại danh mục sở thích</button>}
          <span className="profile-count">{profiles.length} hồ sơ đã tải</span>
        </div>

        {error && <p className="interaction-notice" role="alert">{error}</p>}
        {loading && !profile ? <div className="empty-state" role="status"><DotOrbit /><h2>Đang tải hồ sơ…</h2></div> : profile ? <div className="profile-stage" aria-live="polite">
          <article className="person-card">
            <PhotoBackground className="person-photo" photoUrl={profile.photoUrl} role="img" aria-label={`Ảnh hồ sơ ${profile.name}`}>
              <div className="photo-topline"><span className="verified">✓ Hồ sơ phù hợp</span>
                <div className="photo-menu" ref={menuRef}>
                  <button type="button" className="more-button" aria-expanded={menuOpen} aria-haspopup="menu" onClick={() => setMenuOpen(o => !o)} aria-label="Chặn hoặc báo cáo hồ sơ">···</button>
                  {menuOpen && <div className="more-menu" role="menu">
                    <a role="menuitem" href={'/profile?user=' + encodeURIComponent(profile.id)} onClick={() => setMenuOpen(false)}>Xem hồ sơ</a>
                    <button type="button" role="menuitem" disabled={acting} onClick={() => void blockProfileCard(profile)}>Chặn người này</button>
                    <a role="menuitem" href={'/safety?user=' + encodeURIComponent(profile.id)} onClick={() => setMenuOpen(false)}>Báo cáo hồ sơ</a>
                  </div>}
                </div>
              </div>
              <div className="photo-gradient" />
              <div className="person-caption">{profile.distanceKm != null && <span className="distance-tag">⌖ {profile.distanceKm} km</span>}<h2>{profile.name}<span>, {profile.age}</span></h2><p>{profile.occupation} <i>·</i> {profile.city}</p></div>
              <div className="photo-dots"><i className="on"/><i/><i/></div>
            </PhotoBackground>
            <div className="person-details"><div className="detail-line"><span className="detail-label">ĐÔI ĐIỀU</span><p>{profile.bio || 'Chưa có giới thiệu.'}</p></div><div className="detail-line"><span className="detail-label">SỞ THÍCH</span><div className="interest-list">{profile.interests.map(tag => <span className="interest-tag" key={tag}>{tag}</span>)}</div></div></div>
            <div className="card-actions"><button className="action-pass" disabled={acting} onClick={() => advance('pass')} aria-label="Bỏ qua hồ sơ">×</button><span>Chưa phải lúc</span><button className="action-like" disabled={acting} onClick={() => advance('like')} aria-label="Thích hồ sơ">♡</button><span>Gửi lời thích</span></div>
          </article>
          <div className="stage-aside"><div className="daily-prompt"><span>✳ GỢI Ý HÔM NAY</span><p>Hỏi về điều khiến họ quên mất thời gian.</p></div><div className="privacy-note"><span>♧</span><p>Thông tin của bạn chỉ hiển thị với người phù hợp. Bạn luôn có thể bỏ qua hoặc chặn.</p></div></div>
        </div> : <div className="empty-state"><DotOrbit /><h2>{error ? 'Chưa tải được hồ sơ.' : 'Bạn đã xem hết hồ sơ.'}</h2><p>{error ? 'Hãy hoàn thiện hồ sơ hoặc thử lại sau.' : 'Thử nới bộ lọc để gặp thêm người mới nhé.'}</p>{hasMore ? <button onClick={() => setPage(current => current + 1)}>Tải thêm hồ sơ</button> : <button onClick={() => { setFilters({ ...initialFilters }); setPage(1); setIndex(0); setProfiles([]); }}>Đặt lại bộ lọc</button>}</div>}
        <p className="interaction-notice" aria-live="polite">{notice}</p>
      </section> : connectionsLoading ? <p role="status">Đang tải lời thích và kết nối…</p> :
        <MatchesView matches={matches} received={received} error={connectionsError} onDiscover={() => setActiveTab('discover')} onUnmatch={removeMatch} />}
    </main>
  </div>;
}

function MatchesView({ matches, received, error, onDiscover, onUnmatch }: { matches: MatchSummary[]; received: ReceivedLike[]; error: string; onDiscover: () => void; onUnmatch: (id: string) => void }) {
  return <section className="discover-main matches-main"><div className="discover-heading"><div><p className="sidebar-eyebrow">NHỮNG KẾT NỐI CỦA BẠN</p><h1>Chuyện hay <em>bắt đầu từ đây.</em></h1><p>Các bạn đã cùng gửi lời thích cho nhau.</p></div></div>
    {error && <p className="interaction-notice" role="alert">{error}</p>}
    <section aria-label="Lời thích nhận được" className="received-likes"><h2>Lời thích nhận được</h2>
      {received.length ? <ul>{received.map(person => <li key={person.userId}>
        <a href={'/profile?user=' + person.userId}>{person.displayName} · Xem hồ sơ</a>
      </li>)}</ul> : !error && <p>Chưa có lời thích đang chờ.</p>}
    </section>
    {matches.length ? <div className="matches-grid">{matches.map(match => <article className="match-card" key={match.id}><PhotoBackground className="match-photo" photoUrl={match.otherProfile.photoUrl} /><div className="match-copy"><span className="match-kicker">✳ CÓ DUYÊN GẶP GỠ</span><h2>{match.otherProfile.name}, {match.otherProfile.age}</h2><p>{match.otherProfile.city} · {new Date(match.matchedAt).toLocaleDateString('vi-VN')}</p><a className="btn btn-dark" href={`/chat?conversation=${encodeURIComponent(match.conversationId)}`}>Mở cuộc trò chuyện <span>↗</span></a><button onClick={() => onUnmatch(match.id)}>Bỏ ghép đôi</button></div></article>)}</div> : <div className="empty-state matches-empty"><span>♡</span><h2>{error ? 'Chưa tải được kết nối.' : 'Kết nối của bạn sẽ ở đây.'}</h2><p>{error || 'Hãy khám phá và gửi lời thích nhé.'}</p><button onClick={onDiscover}>Khám phá hồ sơ</button></div>}
  </section>;
}
