import type { DiscoveryProfile } from '../types/discovery';

// Temporary presentation data. Replace with GET /api/v1/discover when Auth and Profiles are ready.
export const demoProfiles: DiscoveryProfile[] = [
  { id: 'mai-nguyen', name: 'Mai', age: 24, city: 'TP. Hồ Chí Minh', occupation: 'Thiết kế sản phẩm', bio: 'Thích những quán cà phê nhỏ, ảnh film và những chuyến đi không cần lịch trình quá chặt.', interests: ['Nhiếp ảnh', 'Du lịch', 'Cà phê'], photoUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=1000&q=85', distanceKm: 3 },
  { id: 'linh-tran', name: 'Linh', age: 25, city: 'TP. Hồ Chí Minh', occupation: 'Kỹ sư phần mềm', bio: 'Cuối tuần thường dành cho leo núi nhẹ, nấu món mới và tìm một playlist hay.', interests: ['Âm nhạc', 'Leo núi', 'Ẩm thực'], photoUrl: 'https://images.unsplash.com/photo-1524504388940-b1c1722653e1?auto=format&fit=crop&w=1000&q=85', distanceKm: 5 },
  { id: 'an-pham', name: 'An', age: 23, city: 'Thủ Đức', occupation: 'Sinh viên kiến trúc', bio: 'Mình thích bảo tàng, cây xanh và những cuộc trò chuyện có chiều sâu.', interests: ['Kiến trúc', 'Sách', 'Nghệ thuật'], photoUrl: 'https://images.unsplash.com/photo-1529139574466-a303027c1d8b?auto=format&fit=crop&w=1000&q=85', distanceKm: 8 },
  { id: 'vy-le', name: 'Vy', age: 26, city: 'TP. Hồ Chí Minh', occupation: 'Chuyên viên marketing', bio: 'Đang học làm bánh sourdough. Có thể giới thiệu bạn vài tiệm bánh ngon.', interests: ['Ẩm thực', 'Cà phê', 'Du lịch'], photoUrl: 'https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=1000&q=85', distanceKm: 6 },
  { id: 'ha-do', name: 'Hà', age: 24, city: 'Bình Thạnh', occupation: 'Giảng viên tiếng Anh', bio: 'Yêu chó, thích chạy bộ buổi sáng và khám phá thành phố bằng cách đi bộ.', interests: ['Chạy bộ', 'Đọc sách', 'Du lịch'], photoUrl: 'https://images.unsplash.com/photo-1526510747491-58f928ec870f?auto=format&fit=crop&w=1000&q=85', distanceKm: 4 },
];
