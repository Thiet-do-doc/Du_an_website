# Cập nhật tương tác và điều hướng — 08/11/2026

- Quà tặng nằm cạnh ô nhập tin nhắn, dùng kết nối SignalR của hội thoại và mã gửi chống trùng. Đường dẫn `/gifts` cũ chuyển sang `/chat`.
- Tab Thông báo có số chưa đọc, lọc chưa đọc, đánh dấu đã đọc và liên kết tới hồ sơ/bài viết/hội thoại. Các sự kiện gồm lượt thích hồ sơ, ghép đôi, tin nhắn, bài viết của người đã ghép đôi, lượt thích bài, bình luận và trả lời.
- Bình luận có trả lời theo luồng, phân trang và một cảm xúc trên mỗi người/bình luận. Bấm nút Thích để thích/gỡ; nhấn giữ 450 ms để chọn Thích, Haha, Tức giận hoặc Buồn. Bàn phím có thể dùng mũi tên lên/xuống để mở, Tab để chọn và Escape để đóng.
- Nút Nhắn tin từ bình luận chỉ mở hội thoại của hai người đã ghép đôi.
- Sau xác nhận đăng ký, chuyển tới Hồ sơ. Điều hướng giao diện giữ thành viên tại đây tới khi có tên hiển thị và ít nhất một sở thích, theo tiêu chí `ProfileService.IsComplete` hiện tại. Trang tài khoản vẫn truy cập được để đăng xuất.
- JWT và kiểm tra phiên sử dụng một vai trò hiệu lực: Admin nếu Identity xác nhận quyền Admin, nếu không là User. Giao diện kiểm tra lại quyền khi gia hạn phiên hoặc quay lại cửa sổ. Admin mở danh sách báo cáo; thành viên xem các báo cáo chính mình đã gửi. Các bảng chặn được bỏ khỏi tab An toàn.

## Dữ liệu và triển khai

Migration: `20261107034902_SocialInteractionsAndNotifications` thêm quan hệ trả lời, bảng cảm xúc, thông tin tác nhân/nội dung thông báo và cho phép thông báo không thuộc hội thoại. Migration chỉ gỡ quan hệ User thừa của những tài khoản đã có Admin; không gỡ Admin.

Build cả API và web trước; sao lưu database, áp migration bằng image API mới, sau đó cập nhật container API/web. Giữ các volume dữ liệu, auth keys, `.env` và cấu hình proxy hiện có. Không hạ migration khi rollback ứng dụng; schema mở rộng vẫn giữ dữ liệu.

## Kiểm chứng

- Backend: 61 kiểm thử qua với PostgreSQL riêng, không bỏ qua bài nào.
- Frontend: 8 kiểm thử qua; TypeScript và Vite production build qua.
- Trình duyệt: gửi quà trong hội thoại; mở bài từ thông báo; danh sách báo cáo không có bảng chặn; nhấn giữ mở bộ cảm xúc và chọn cảm xúc; tài khoản mới bị chuyển tới hồ sơ, lưu tên/sở thích sẽ hiện nút tiếp tục.
- Kiểm thử API bao phủ trả lời sai bài viết, đổi/gỡ cảm xúc, lọc người bị chặn, quyền đọc thông báo/báo cáo, thông báo người đã ghép đôi đăng bài, tránh thông báo thích lặp và giữ Admin là vai trò duy nhất.
