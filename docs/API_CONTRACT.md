# Hợp đồng API hiện có

Base URL local: `http://localhost:5080`.

| Phương thức | Đường dẫn | Kết quả |
|---|---|---|
| GET | /api/v1/health | 200, status `ok`; chỉ cho biết API đang chạy |
| GET | /api/v1/health/database | 200 `connected`; 503 `not_configured` hoặc `unavailable` |
| GET | /openapi/v1.json | OpenAPI JSON, chỉ bật trong Development |

Ví dụ body health:
```json
{"status":"ok","message":"DatingApp API đang hoạt động.","checkedAt":"2026-10-12T14:00:00+00:00"}
```
Thời gian trên là ví dụ; server trả thời gian hiện tại khi chạy.

Chưa có Swagger UI. Có thể import URL OpenAPI vào Postman hoặc mở các GET trực tiếp.
Profile/match/chat chưa có API; auth và SafetyAdmin được ghi ở phần dưới.

## SafetyAdmin

| Phương thức | Đường dẫn | Quyền / dữ liệu |
|---|---|---|
| POST / DELETE | `/api/v1/blocks/{targetUserId}` | Người dùng đã đăng nhập; block hai chiều được kiểm tra qua `IBlockPolicy` |
| POST | `/api/v1/reports` | Người dùng đã đăng nhập; `targetType`, `targetId`, `reason`, `description` |
| GET | `/api/v1/admin/reports` | Role `Admin`; lọc `status`, `page`, `pageSize` |
| GET / PATCH | `/api/v1/admin/reports/{id}` | Role `Admin`; cập nhật status kèm ghi chú xử lý |
| PATCH | `/api/v1/admin/users/{id}/status` | Role `Admin`; body `{ "isActive": false, "reason": "..." }` |

Enum JSON dùng tên chuỗi. Report User hoạt động; report Message/Post trả 501 cho
đến khi TV4/TV2 cung cấp kiểm tra đối tượng và quyền. Admin report không trả nội
dung chat. TV1 cần cấu hình role `Admin` trong Auth.

## Điểm tích hợp TV5 cần nhóm xác nhận

Các mục dưới đây là yêu cầu tích hợp tiếp theo, chưa khẳng định luồng xuyên module
đã hoạt động. Giữ DTO và tên service hiện có ở phần SafetyAdmin phía trên.

- TV1 xác nhận claim UserId, cấp role `Admin` và `IAccountStatusService`. Người gọi
  lấy từ principal đã xác thực; không nhận `actorUserId` hay `reporterUserId` từ body.
- TV3/TV4 dùng chung `IBlockPolicy.CanInteractAsync(a, b)` trước khám phá/tạo match
  và trước gửi/nhận tin trên cả REST lẫn Hub. TV3 chốt việc hủy match/conversation
  đang hoạt động khi block; unblock không dựng lại match.
- TV2/TV4 cung cấp kiểm tra đối tượng Message/Post tồn tại và quyền tham chiếu của
  reporter trước khi bật hai loại report này. Request hiện dùng `description`,
  không đổi thành `details` chỉ dựa trên bản đề xuất cũ.
- Admin list/detail không trả nội dung chat. Nếu cần xem message bị report, TV4
  phải cung cấp contract đọc giới hạn đúng message và ghi audit mỗi lần truy cập;
  không cung cấp quyền đọc toàn bộ lịch sử chat.
- Chốt DTO/status, kiểm thử sai quyền và luồng end-to-end trước khi phát hành các
  tích hợp mới; không tạo API hoặc bảng trùng với module khác.

Quy ước: camelCase cho JSON, UTC cho thời gian, UUID cho định danh; DTO không trả
password hash, token bí mật hoặc ngày sinh của người khác. API nghiệp vụ sẽ dùng
Problem Details cho lỗi; health readiness là ngoại lệ có body health riêng.
