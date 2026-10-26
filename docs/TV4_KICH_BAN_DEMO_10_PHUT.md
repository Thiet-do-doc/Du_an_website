# Kịch bản demo 10 phút — Thành viên 4

Bản demo sử dụng tài khoản A/B/C giả, đã match A–B. Trước buổi thuyết trình cần chạy đủ bước, chụp screenshot thật và đánh dấu lỗi đã sửa.

| Thời lượng | Thao tác | Điểm kiến thức nói với giảng viên |
|---|---|---|
| 0–1 phút | Giới thiệu module, mở project/ERD | TV4 phụ trách 3 bảng dữ liệu, SignalR, React |
| 1–2 phút | Chạy API, mở `GET /api/v1/health` | API và DB PostgreSQL |
| 2–3 phút | Mở A trên Chrome, B ẩn danh, gửi tin | ChatHub → ChatService → DB → MessageCreated |
| 3–4 phút | Refresh trình duyệt cả A/B | Có lưu lịch sử, không chỉ giữ state React |
| 4–5 phút | Chọn C, thử gọi API của conversation A/B | Backend từ chối; không dựa vào ẩn UI |
| 5–6 phút | A gửi sau khi B offline, B kết nối lại | SignalR reconnect và tải bù qua afterId |
| 6–7 phút | Gửi lại cùng clientMessageId qua smoke script | DB unique + idempotency, tránh trùng tin |
| 7–8 phút | Chặn A→B, thử gửi | Hub không cho gửi dù đã kết nối trước |
| 8–9 phút | Tạo match A–C và mở tab thông báo | MatchCreated/NotificationCreated, database |
| 9–10 phút | Cho xem test/PR và kết luận | T4.1–T4.5; biết hạn chế/bước merge |

**Ảnh cần chụp:** cửa sổ A/B, bảng SQL Messages/Notifications, Network offline, màn hình C bị 404, tab Notifications và kết quả `npm run test:tv4`/`dotnet test`.

**Một câu giới thiệu ngắn để tự luyện:** "Phần của em là module ChatNotifications. Em thiết kế dữ liệu chat/thông báo, lập trình SignalR và giao diện React; backend kiểm tra quyền trước mọi lần gửi, lưu database trước realtime, khôi phục sau mất mạng và chống gửi trùng bằng clientMessageId."

**Tránh nhầm lẫn:** Bộ ZIP chạy minh họa với DemoUser/DemoMatch/DemoBlock. Khi lên nhánh chung, nhóm phải thay bằng Identity của TV1, Match của TV3 và BlockPolicy của TV5; cần chạy lại kiểm thử tích hợp. Đừng trình bày bản demo độc lập là đã tích hợp production.
