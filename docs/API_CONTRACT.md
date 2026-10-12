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

Quy ước: camelCase cho JSON, UTC cho thời gian, UUID cho định danh; DTO không trả
password hash, token bí mật hoặc ngày sinh của người khác. API nghiệp vụ sẽ dùng
Problem Details cho lỗi; health readiness là ngoại lệ có body health riêng.
