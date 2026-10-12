# ChatNotifications frontend
Chủ trì TV4; người dự phòng TV5.
SignalR, lịch sử chat, chống gửi trùng, reconnect và thông báo.

- `pages/`: màn hình cấp route.
- `components/`: thành phần chỉ dùng trong module.
- `api/`: hàm gọi API; tái sử dụng client ở `shared/api/`.
- `types/`: TypeScript DTO tương ứng với backend.

Chưa có chức năng nghiệp vụ. Thống nhất auth và route trước khi nối vào app.
