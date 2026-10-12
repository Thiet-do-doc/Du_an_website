# ChatNotifications — TV4

Đã có code cho ChatHub, Conversations/Messages/Notifications, DTOs, services, keyset pagination, tests và frontend tương ứng. Đây là bản **demo local** dùng `DemoChatPermissions` (cùng DbContext) để kiểm thử độc lập khi Auth, Match, Block thật của nhóm chưa hoàn thành.

**Để chạy** đọc `README_TV4_VISUAL_STUDIO.md` ở gốc repo. API thuộc `/api/v1/conversations`, `/api/v1/notifications`; Hub `/hubs/chat`. Không cho phép production dùng Demo* hoặc `/api/v1/dev/*`.

Tạo migration sau khi phối hợp TV1 để tránh xung đột migration trên cùng AppDbContext. Xem thêm `docs/TV4_API_VA_TICH_HOP.md`.
