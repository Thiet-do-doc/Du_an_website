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
API Auth thật đã có: xem [Auth API và Postman](AUTH_TESTING.md).
Profiles có API xem/sửa hồ sơ cá nhân, hồ sơ công khai, danh mục sở thích và
upload/đặt/xóa/sắp xếp ảnh; hợp đồng chi tiết nằm trong
[`Modules/Profiles/README.md`](../backend/DatingApp.Api/Modules/Profiles/README.md).
Match/chat chưa có API nghiệp vụ tích hợp.

Quy ước: camelCase cho JSON, UTC cho thời gian, UUID cho định danh; DTO không trả
password hash, token bí mật hoặc ngày sinh của người khác. API nghiệp vụ sẽ dùng
Problem Details cho lỗi; health readiness là ngoại lệ có body health riêng.
