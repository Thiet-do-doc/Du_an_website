# mylove — cập nhật giao diện và bảng tin (27/10/2026)

## Giao diện
- Khách chỉ thấy banner và form tài khoản; mọi trang thành viên yêu cầu phiên đăng nhập.
- Đăng nhập thành công vào `/` (bảng tin). Ngày sinh giữ trong đăng ký, không có cửa xác nhận tuổi riêng.
- Form đăng ký vẫn yêu cầu xác nhận email. Chế độ Development lưu mail tại `.artifacts/mail`.
- Menu chung: Trang chủ, Khám phá, Trò chuyện, Quà tặng, Ví của tôi, Hồ sơ, An toàn; Tài khoản chứa đăng xuất.
- Tên hiển thị mylove, màu hồng pastel, bố cục đáp ứng màn hình nhỏ.
- Đặt ảnh nhóm tại `frontend/public/images/team.jpg`; banner tự dùng ảnh. Khi chưa có ảnh, dùng thiệp trang trí.

## Bảng tin cơ bản
- GET `/api/v1/posts?page=1`: 20 bài/trang, mới nhất trước, `items` và `hasMore`.
- POST `/api/v1/posts`: `{ "content": "..." }`, văn bản 1–2000 ký tự, cần lưu tên hiển thị trong Hồ sơ.
- DELETE `/api/v1/posts/{id}`: chỉ tác giả được xóa; không cho client chọn người đăng.
- Tất cả endpoint cần JWT hợp lệ; không công khai email/ngày sinh. Lọc tài khoản ngừng hoạt động và chặn hai chiều.
- Bài viết hiển thị cho thành viên kể cả khi tác giả tắt khám phá hồ sơ, theo thông báo ở form đăng bài. Bài viết là nội dung chia sẻ chủ động riêng với cài đặt khám phá.
- Chưa bổ sung ảnh bài viết, bình luận, lượt thích hoặc chia sẻ bài viết.
- Quà tặng và ví mới là giao diện “Sắp ra mắt”; không tạo số dư giả hoặc thực hiện giao dịch tiền.

## Chạy và cập nhật database
Từ thư mục gốc, với connection string và Jwt:Key local đã thiết lập:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --project backend/DatingApp.Api
dotnet run --project backend/DatingApp.Api --launch-profile http
# Terminal khác
npm.cmd --prefix frontend run dev
```

Migration mới `AddCommunityPosts` chỉ thêm bảng Posts và index/FK. Máy này trước đó mới áp dụng InitialIdentity và AuthSessions, khiến các trang hồ sơ/chat/khám phá trả 500. Đã sao lưu trước khi áp dụng toàn bộ migration còn thiếu; bản sao lưu trong `.artifacts/mylove-backup-*.dump` (không commit).

## Kiểm chứng
- Frontend production build đạt; 8/8 frontend tests đạt.
- Backend Release build không lỗi/cảnh báo; 54/54 tests đạt, không bỏ qua, trên database riêng `datingapp_mylove_test_20261027`.
- PostsFlowTests kiểm tra JWT, tác giả từ phiên đăng nhập, yêu cầu tên hiển thị, nội dung rỗng/quá dài, quyền xóa và chặn hai chiều.
- Trình duyệt: đăng nhập tài khoản giả → bảng tin, đăng bài thành công, menu An toàn/Quà tặng/Ví. Dữ liệu giả chỉ nằm trong database thử.

### Hoàn tất kiểm tra ngày 28/10/2026
- Khởi động lại bản chính: frontend tại `http://127.0.0.1:5173/`, API cổng 5080; HTTP trang chủ và health database đều 200.
- Kiểm tra trực quan trang chào tại 1440×900 và 390×844. Trên màn hình nhỏ, form tài khoản nằm trước banner ảnh để dễ truy cập.
- Truy cập thẳng `/chat` khi chưa đăng nhập hiển thị form đăng nhập, không render thanh điều hướng thành viên.
- Form đăng ký có ngày sinh; trang đăng nhập không có bước hỏi tuổi.
- Server thử cổng 5081/5174 đã dừng; dữ liệu thử được giữ riêng. Bản chính đã được mở để bàn giao.
- Ảnh giao diện lưu tại `.artifacts/mylove-welcome.png`.
