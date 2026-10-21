import { useCallback, useEffect, useState } from 'react';
import { ProtectedRoute } from '../auth/AuthProvider';
import { deletePhoto, getInterests, getMyProfile, reorderPhotos, saveProfile, setAvatar, uploadPhoto } from './api';
import type { Interest, Profile } from './types';
import './profiles.css';

function ProfileEditor() {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [interests, setInterests] = useState<Interest[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [name, setName] = useState(''); const [bio, setBio] = useState(''); const [city, setCity] = useState('');
  const [gender, setGender] = useState(''); const [discoverable, setDiscoverable] = useState(true);
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const load = useCallback(async () => {
    setError(''); setLoading(true);
    try {
      const [p, choices] = await Promise.all([getMyProfile(), getInterests()]);
      setProfile(p); setInterests(choices); setName(p.displayName); setBio(p.bio ?? ''); setCity(p.city ?? '');
      setGender(p.gender ?? ''); setDiscoverable(p.isDiscoverable); setSelected(p.interests.map(x => x.id));
    } catch { setError('Không tải được hồ sơ. Hãy kiểm tra kết nối và thử lại.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  async function run(action: () => Promise<unknown>, message: string) {
    setBusy(true); setError(''); setNotice('');
    try { await action(); await load(); setNotice(message); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không thể hoàn tất thao tác.'); }
    finally { setBusy(false); }
  }
  async function submit(event: React.FormEvent) {
    event.preventDefault(); await run(() => saveProfile({ displayName: name, bio, city, gender, isDiscoverable: discoverable, interestIds: selected }), 'Đã lưu hồ sơ.');
  }
  async function upload(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]; event.target.value = '';
    if (file) await run(() => uploadPhoto(file), 'Đã thêm ảnh.');
  }
  async function move(index: number, offset: number) {
    if (!profile) return;
    const photos = [...profile.photos].sort((a, b) => a.sortOrder - b.sortOrder);
    const target = index + offset; if (target < 0 || target >= photos.length) return;
    [photos[index], photos[target]] = [photos[target], photos[index]];
    await run(() => reorderPhotos(photos.map(x => x.id)), 'Đã cập nhật thứ tự ảnh.');
  }
  if (loading) return <p role="status">Đang tải hồ sơ…</p>;
  return <div className="profile-page">
    <div className="profile-heading"><div><p className="eyebrow">HỒ SƠ CỦA BẠN</p><h1>Hãy để câu chuyện của bạn bắt đầu.</h1>
      <p className="profile-lead">Hoàn thiện hồ sơ để mọi người hiểu thêm về bạn. Ngày sinh và email không hiển thị công khai.</p></div>
      {profile?.isComplete && <span className="profile-badge">Hồ sơ hoàn thiện</span>}</div>
    {error && <p className="profile-alert" role="alert">{error}</p>}{notice && <p className="profile-notice" role="status">{notice}</p>}
    <form className="profile-card" onSubmit={submit}>
      <h2>Giới thiệu</h2>
      <label>Tên hiển thị<input required maxLength={60} value={name} onChange={e => setName(e.target.value)} /></label>
      <label>Giới thiệu<textarea maxLength={500} rows={4} value={bio} onChange={e => setBio(e.target.value)} placeholder="Một vài điều bạn muốn chia sẻ…" /><small>{bio.length}/500</small></label>
      <div className="profile-fields"><label>Thành phố<input maxLength={80} value={city} onChange={e => setCity(e.target.value)} placeholder="Ví dụ: Hà Nội" /></label>
        <label>Giới tính<select value={gender} onChange={e => setGender(e.target.value)}><option value="">Chưa muốn chia sẻ</option><option value="Nữ">Nữ</option><option value="Nam">Nam</option><option value="Khác">Khác</option></select></label></div>
      <fieldset><legend>Sở thích <small>Chọn tối đa 20</small></legend><div className="interest-list">{interests.map(item => <label key={item.id} className={selected.includes(item.id) ? 'interest selected' : 'interest'}>
        <input type="checkbox" checked={selected.includes(item.id)} disabled={!selected.includes(item.id) && selected.length >= 20} onChange={e => setSelected(v => e.target.checked ? [...v, item.id] : v.filter(id => id !== item.id))} />{item.name}</label>)}</div></fieldset>
      <label className="discoverable"><input type="checkbox" checked={discoverable} onChange={e => setDiscoverable(e.target.checked)} />Cho phép hồ sơ xuất hiện trong khám phá</label>
      <button className="btn btn-dark" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button>
    </form>
    <section className="profile-card" aria-labelledby="photos-heading"><div className="profile-section-head"><div><h2 id="photos-heading">Ảnh của bạn</h2><p>Tối đa 6 ảnh · JPEG, PNG hoặc WebP · tối đa 5 MB mỗi ảnh</p></div>
      <label className="btn btn-outline-dark upload-label">Thêm ảnh<input type="file" accept="image/jpeg,image/png,image/webp" onChange={upload} disabled={busy || (profile?.photos.length ?? 0) >= 6} /></label></div>
      <div className="photo-grid">{profile?.photos.length ? [...profile.photos].sort((a, b) => a.sortOrder - b.sortOrder).map((photo, i) => <article className="photo-item" key={photo.id}>
        <img src={photo.url} alt={photo.isAvatar ? 'Ảnh đại diện' : `Ảnh hồ sơ ${i + 1}`} />{photo.isAvatar && <span className="avatar-label">Ảnh đại diện</span>}
        <div className="photo-actions"><button type="button" onClick={() => void run(() => setAvatar(photo.id), 'Đã chọn ảnh đại diện.')} disabled={busy || photo.isAvatar}>Chọn đại diện</button>
          <button type="button" onClick={() => void move(i, -1)} disabled={busy || i === 0} aria-label="Chuyển ảnh lên trước">↑</button>
          <button type="button" onClick={() => void move(i, 1)} disabled={busy || i === (profile.photos.length - 1)} aria-label="Chuyển ảnh xuống sau">↓</button>
          <button type="button" onClick={() => void run(() => deletePhoto(photo.id), 'Đã xóa ảnh.')}>Xóa</button></div></article>) : <div className="photo-placeholder" role="img" aria-label="Ảnh đại diện mặc định"><span aria-hidden="true">♥</span><p>Ảnh đại diện sẽ xuất hiện ở đây</p></div>}</div>
    </section>
  </div>;
}

export default function ProfilePage() { return <ProtectedRoute><ProfileEditor /></ProtectedRoute>; }
