import { useMemo, useState } from 'react';
import { demoProfiles } from '../api/demoProfiles';
import type { DiscoveryFilters, DiscoveryProfile, Match } from '../types/discovery';

type Props = { onBack: () => void };
const initialFilters: DiscoveryFilters = { minAge: 18, maxAge: 35, city: 'Tất cả khu vực', interest: 'Tất cả sở thích' };

export default function DiscoveryPage({ onBack }: Props) {
  const [filters, setFilters] = useState(initialFilters);
  const [index, setIndex] = useState(0);
  const [liked, setLiked] = useState<string[]>([]);
  const [passed, setPassed] = useState<string[]>([]);
  const [matches, setMatches] = useState<Match[]>([]);
  const [activeTab, setActiveTab] = useState<'discover' | 'matches'>('discover');
  const [notice, setNotice] = useState('');

  const profiles = useMemo(() => demoProfiles.filter(profile =>
    profile.age >= filters.minAge && profile.age <= filters.maxAge &&
    (filters.city === 'Tất cả khu vực' || profile.city === filters.city) &&
    (filters.interest === 'Tất cả sở thích' || profile.interests.includes(filters.interest)) &&
    !passed.includes(profile.id) && !liked.includes(profile.id)), [filters, liked, passed]);
  const profile = profiles[index];

  function advance(action: 'like' | 'pass') {
    if (!profile) return;
    if (action === 'like') {
      setLiked(current => [...current, profile.id]);
      // Demo-only mutual likes make the match and chat path easy to preview.
      if (['mai-nguyen', 'vy-le'].includes(profile.id)) {
        setMatches(current => [...current, { id: `match-${profile.id}`, profile, matchedAt: new Date().toISOString() }]);
        setNotice(`It's a match! Bạn và ${profile.name} cùng thích nhau.`);
      } else setNotice(`Bạn đã thích ${profile.name}.`);
    } else {
      setPassed(current => [...current, profile.id]);
      setNotice('Đã bỏ qua hồ sơ này.');
    }
    setIndex(0);
  }

  function updateFilter<K extends keyof DiscoveryFilters>(key: K, value: DiscoveryFilters[K]) {
    setFilters(current => ({ ...current, [key]: value }));
    setIndex(0);
  }

  return <div className="dating-shell">
    <header className="dating-topbar"><button className="wordmark" onClick={onBack} aria-label="Về trang chính">dear<span>you</span><i>✳</i></button>
      <nav className="dating-nav" aria-label="Điều hướng chính">
        <button className={activeTab === 'discover' ? 'nav-item selected' : 'nav-item'} onClick={() => setActiveTab('discover')}>Khám phá</button>
        <button className={activeTab === 'matches' ? 'nav-item selected' : 'nav-item'} onClick={() => setActiveTab('matches')}>Kết nối <span className="nav-count">{matches.length}</span></button>
      </nav>
      <button className="profile-chip" aria-label="Hồ sơ của bạn"><span className="profile-avatar">T</span><span>Thuỳ</span><span className="chevron">⌄</span></button>
    </header>

    <main className="discovery-layout">
      <aside className="discovery-sidebar">
        <p className="sidebar-eyebrow">KHÔNG GIAN CỦA BẠN</p>
        <button className={activeTab === 'discover' ? 'side-link active' : 'side-link'} onClick={() => setActiveTab('discover')}><span>✳</span> Khám phá</button>
        <button className={activeTab === 'matches' ? 'side-link active' : 'side-link'} onClick={() => setActiveTab('matches')}><span>♡</span> Lượt thích & kết nối <b>{matches.length}</b></button>
        <div className="sidebar-note"><span className="note-spark">✳</span><p>Một kết nối tốt bắt đầu bằng sự tò mò chân thành.</p></div>
        <p className="sidebar-foot">Bản xem trước · dữ liệu minh hoạ</p>
      </aside>

      {activeTab === 'discover' ? <section className="discover-main">
        <div className="discover-heading"><div><p className="sidebar-eyebrow">THỨ HAI, 12 THÁNG 10</p><h1>Gặp gỡ <em>người mới.</em></h1><p>Những người có thể cùng bạn tạo nên một câu chuyện hay.</p></div>
          <div className="online-pill"><span /> Đang có người mới</div></div>
        <div className="filter-row" aria-label="Bộ lọc khám phá">
          <label>Khu vực<select value={filters.city} onChange={e => updateFilter('city', e.target.value)}><option>Tất cả khu vực</option><option>TP. Hồ Chí Minh</option><option>Thủ Đức</option><option>Bình Thạnh</option></select></label>
          <label>Độ tuổi<select value={`${filters.minAge}-${filters.maxAge}`} onChange={e => { const [minAge, maxAge] = e.target.value.split('-').map(Number); setFilters(f => ({ ...f, minAge, maxAge })); setIndex(0); }}><option value="18-35">18 – 35</option><option value="18-25">18 – 25</option><option value="23-30">23 – 30</option><option value="25-35">25 – 35</option></select></label>
          <label>Sở thích<select value={filters.interest} onChange={e => updateFilter('interest', e.target.value)}><option>Tất cả sở thích</option>{Array.from(new Set(demoProfiles.flatMap(item => item.interests))).sort().map(item => <option key={item}>{item}</option>)}</select></label>
          <span className="profile-count">{profiles.length} hồ sơ phù hợp</span>
        </div>

        {profile ? <div className="profile-stage" aria-live="polite">
          <article className="person-card">
            <div className="person-photo" style={{ backgroundImage: `url("${profile.photoUrl}")` }} role="img" aria-label={`Ảnh hồ sơ ${profile.name}`}>
              <div className="photo-topline"><span className="verified">✓ Đã xác minh email</span><button className="more-button" aria-label="Tuỳ chọn hồ sơ">···</button></div>
              <div className="photo-gradient" />
              <div className="person-caption"><span className="distance-tag">⌖ {profile.distanceKm} km</span><h2>{profile.name}<span>, {profile.age}</span></h2><p>{profile.occupation} <i>·</i> {profile.city}</p></div>
              <div className="photo-dots"><i className="on"/><i/><i/></div>
            </div>
            <div className="person-details"><div className="detail-line"><span className="detail-label">ĐÔI ĐIỀU</span><p>{profile.bio}</p></div><div className="detail-line"><span className="detail-label">SỞ THÍCH</span><div className="interest-list">{profile.interests.map(tag => <span className="interest-tag" key={tag}>{tag}</span>)}</div></div></div>
            <div className="card-actions"><button className="action-pass" onClick={() => advance('pass')} aria-label="Bỏ qua hồ sơ">×</button><span>Chưa phải lúc</span><button className="action-like" onClick={() => advance('like')} aria-label="Thích hồ sơ">♡</button><span>Gửi lời thích</span></div>
          </article>
          <div className="stage-aside"><div className="daily-prompt"><span>✳ GỢI Ý HÔM NAY</span><p>Hỏi về điều khiến họ quên mất thời gian.</p></div><div className="privacy-note"><span>♧</span><p>Thông tin của bạn chỉ hiển thị với người phù hợp. Bạn luôn có thể bỏ qua hoặc chặn.</p></div></div>
        </div> : <div className="empty-state"><span>✳</span><h2>Hôm nay bạn đã xem hết rồi.</h2><p>Thử nới bộ lọc để gặp thêm người mới nhé.</p><button onClick={() => { setFilters(initialFilters); setPassed([]); setLiked([]); setIndex(0); }}>Xem lại hồ sơ</button></div>}
        <p className="interaction-notice" aria-live="polite">{notice}</p>
        <div className="demo-banner"><span>ⓘ</span><p>Đây là bản giao diện minh hoạ. Like, ghép đôi và bộ lọc hiện dùng dữ liệu mẫu trên trình duyệt.</p></div>
      </section> : <MatchesView matches={matches} onDiscover={() => setActiveTab('discover')} />}
    </main>
  </div>;
}

function MatchesView({ matches, onDiscover }: { matches: Match[]; onDiscover: () => void }) {
  return <section className="discover-main matches-main"><div className="discover-heading"><div><p className="sidebar-eyebrow">NHỮNG KẾT NỐI CỦA BẠN</p><h1>Chuyện hay <em>bắt đầu từ đây.</em></h1><p>Các bạn đã cùng gửi lời thích cho nhau.</p></div></div>
    {matches.length ? <div className="matches-grid">{matches.map(match => <article className="match-card" key={match.id}><div className="match-photo" style={{ backgroundImage: `url("${match.profile.photoUrl}")` }} /><div className="match-copy"><span className="match-kicker">✳ CÓ DUYÊN GẶP GỠ</span><h2>{match.profile.name}, {match.profile.age}</h2><p>{match.profile.city}</p><button disabled title="Chat sẽ được nối với module TV4">Sắp có thể trò chuyện <span>↗</span></button></div></article>)}</div> : <div className="empty-state matches-empty"><span>♡</span><h2>Kết nối của bạn sẽ ở đây.</h2><p>Hãy khám phá và gửi lời thích cho ai đó nhé.</p><button onClick={onDiscover}>Khám phá hồ sơ</button></div>}
  </section>;
}
