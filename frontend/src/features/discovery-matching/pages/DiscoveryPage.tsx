import { PhotoBackground } from '../../profiles/PrivatePhoto';
import { NavIcon } from '../../../app/NavIcon';
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
  const [filtersOpen, setFiltersOpen] = useState(false);
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
  const suggestions = profiles.slice(index + 1, index + 3);

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
      setIndex(index + 1);
      if (index + 1 === profiles.length && hasMore) setPage(current => current + 1);
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
      setIndex(index + 1);
      if (index + 1 === profiles.length && hasMore) setPage(current => current + 1);
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

  return <section className="dating-shell discovery-page">
    <header className="discovery-header">
      <div><h1>Khám phá</h1><p>Gặp những người có chung sở thích với bạn.</p></div>
      <div className="discovery-tabs" role="group" aria-label="Chế độ khám phá">
        <button type="button" aria-pressed={activeTab === 'discover'} onClick={() => { setError(''); setActiveTab('discover'); }}>Gợi ý</button>
        <button type="button" aria-pressed={activeTab === 'matches'} onClick={() => { setError(''); setActiveTab('matches'); }}>
          Ghép đôi {connectionCount !== null && connectionCount > 0 && <span>{connectionCount}</span>}
        </button>
      </div>
    </header>

      {activeTab === 'discover' ? <>
        <div className="discovery-filter-bar">
          <span className="discovery-filter-label">Bộ lọc</span>
          <button type="button" className="btn btn-outline-dark discovery-filter-toggle" aria-expanded={filtersOpen}
            aria-controls="discovery-filters" onClick={() => setFiltersOpen(open => !open)}>Bộ lọc</button>
          <div id="discovery-filters" className="discovery-filters" data-open={filtersOpen} aria-label="Bộ lọc khám phá">
          <label>Khu vực<select value={filters.city} onChange={e => updateFilter('city', e.target.value)}><option>Tất cả khu vực</option><option>TP. Hồ Chí Minh</option><option>Thủ Đức</option><option>Bình Thạnh</option></select></label>
          <label>Độ tuổi<select value={`${filters.minAge}-${filters.maxAge}`} onChange={e => { const [minAge, maxAge] = e.target.value.split('-').map(Number); setFilters(f => ({ ...f, minAge, maxAge })); setPage(1); setIndex(0); setProfiles([]); }}><option value="18-35">18 – 35</option><option value="18-25">18 – 25</option><option value="23-30">23 – 30</option><option value="25-35">25 – 35</option></select></label>
          <label>Sở thích<select aria-label="Sở thích" value={filters.interest} onChange={e => updateFilter('interest', e.target.value)}><option>Tất cả sở thích</option>{interests.map(item => <option key={item.id} value={item.name}>{item.name}</option>)}</select></label>
          {catalogError && <button type="button" onClick={() => setCatalogAttempt(n => n + 1)}>Tải lại danh mục sở thích</button>}
          </div>
          {!loading && <span className="discovery-result-count">{Math.max(0, profiles.length - index)} hồ sơ</span>}
        </div>

        {error && <p className="interaction-notice" role="alert">{error}</p>}
        {loading && !profile ? <div className="empty-state" role="status"><DotOrbit /><h2>Đang tải hồ sơ…</h2></div> : profile ? <>
          <article className="discovery-spotlight" aria-live="polite">
            <div className="discovery-portrait">
              <PhotoBackground className="discovery-image" photoUrl={profile.photoUrl} role="img" aria-label={`Ảnh hồ sơ ${profile.name}`} />
              {profile.distanceKm != null && <span className="discovery-distance"><NavIcon name="location" />Cách bạn {profile.distanceKm} km</span>}
            </div>
            <div className="discovery-detail">
              <div className="discovery-person-header">
                <div>
                  <h2>{profile.name}<span>, {profile.age}</span></h2>
                  {profile.city && <p><NavIcon name="location" />{profile.city}</p>}
                  {profile.occupation && <p>{profile.occupation}</p>}
                </div>
                <div className="photo-menu" ref={menuRef}>
                  <button type="button" className="more-button" aria-expanded={menuOpen} aria-haspopup="menu" onClick={() => setMenuOpen(o => !o)} aria-label="Chặn hoặc báo cáo hồ sơ">···</button>
                  {menuOpen && <div className="more-menu" role="menu">
                    <a role="menuitem" href={'/profile?user=' + encodeURIComponent(profile.id)} onClick={() => setMenuOpen(false)}>Xem hồ sơ</a>
                    <button type="button" role="menuitem" disabled={acting} onClick={() => void blockProfileCard(profile)}>Chặn người này</button>
                    <a role="menuitem" href={'/safety?user=' + encodeURIComponent(profile.id)} onClick={() => setMenuOpen(false)}>Báo cáo hồ sơ</a>
                  </div>}
                </div>
              </div>
              <div className="discovery-actions">
                <button type="button" className="btn btn-outline-dark" disabled={acting || loading} onClick={() => void advance('pass')}>Bỏ qua</button>
                <button type="button" className="btn btn-dark" disabled={acting || loading} onClick={() => void advance('like')}><NavIcon name="heart" />Gửi lời thích</button>
              </div>
              <div className="discovery-about"><h3>Giới thiệu</h3><p>{profile.bio || 'Chưa có giới thiệu.'}</p>
                <div className="interest-list">{profile.interests.map(tag => <span className="interest-tag" key={tag}>{tag}</span>)}</div>
              </div>
              <a className="discovery-profile-link text-link" href={'/profile?user=' + encodeURIComponent(profile.id)}>Xem hồ sơ đầy đủ →</a>
            </div>
          </article>
          {suggestions.length > 0 && <section className="discovery-suggestions"><h2>Xem thêm hồ sơ</h2><div>
            {suggestions.map(person => <a key={person.id} href={'/profile?user=' + encodeURIComponent(person.id)}>
              <PhotoBackground className="suggestion-photo" photoUrl={person.photoUrl} />
              <span><strong>{person.name}, {person.age}</strong><small>{person.city}</small></span><span aria-hidden="true">›</span>
            </a>)}
          </div></section>}
        </> : <div className="empty-state"><h2>{error ? 'Chưa tải được hồ sơ.' : 'Bạn đã xem hết hồ sơ.'}</h2><p>{error ? 'Hãy hoàn thiện hồ sơ hoặc thử lại sau.' : 'Thử nới bộ lọc để gặp thêm người mới nhé.'}</p>{hasMore ? <button onClick={() => setPage(current => current + 1)}>Tải thêm hồ sơ</button> : <button onClick={() => { setFilters({ ...initialFilters }); setPage(1); setIndex(0); setProfiles([]); }}>Đặt lại bộ lọc</button>}</div>}
        <p className="interaction-notice" aria-live="polite">{notice}</p>
      </> : connectionsLoading ? <p role="status">Đang tải lời thích và kết nối…</p> :
        <MatchesView matches={matches} received={received} error={connectionsError} onDiscover={() => setActiveTab('discover')} onUnmatch={removeMatch} />}
  </section>;
}

function MatchesView({ matches, received, error, onDiscover, onUnmatch }: { matches: MatchSummary[]; received: ReceivedLike[]; error: string; onDiscover: () => void; onUnmatch: (id: string) => void }) {
  return <section className="discover-main matches-main">
    {error && <p className="interaction-notice" role="alert">{error}</p>}
    <section aria-label="Lời thích nhận được" className="received-likes"><h2>Lời thích nhận được</h2>
      {received.length ? <ul>{received.map(person => <li key={person.userId}>
        <a href={'/profile?user=' + person.userId}>{person.displayName} · Xem hồ sơ</a>
      </li>)}</ul> : !error && <p>Chưa có lời thích đang chờ.</p>}
    </section>
    {matches.length ? <div className="matches-grid">{matches.map(match => <article className="match-card" key={match.id}><PhotoBackground className="match-photo" photoUrl={match.otherProfile.photoUrl} /><div className="match-copy"><h2>{match.otherProfile.name}, {match.otherProfile.age}</h2><p>{match.otherProfile.city} · {new Date(match.matchedAt).toLocaleDateString('vi-VN')}</p><a className="btn btn-dark" href={`/chat?conversation=${encodeURIComponent(match.conversationId)}`}>Mở cuộc trò chuyện <span>↗</span></a><button onClick={() => onUnmatch(match.id)}>Bỏ ghép đôi</button></div></article>)}</div> : <div className="empty-state matches-empty"><h2>{error ? 'Chưa tải được kết nối.' : 'Chưa có lượt ghép đôi.'}</h2><p>{error || 'Khi cả hai cùng gửi lời thích, hồ sơ sẽ xuất hiện ở đây.'}</p><button onClick={onDiscover}>Khám phá hồ sơ</button></div>}
  </section>;
}
