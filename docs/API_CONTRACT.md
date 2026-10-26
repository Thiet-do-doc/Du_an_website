> Cập nhật 26/10/2026: các module đã được nối trong bản tích hợp. Xem [INTEGRATION_STATUS.md](INTEGRATION_STATUS.md) cho schema và hành vi hiện hành. Nội dung bàn giao bên dưới được giữ để đối chiếu lịch sử.

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

Profile API có trong `Modules/Profiles`; Discovery/Matching được mô tả dưới đây. Block lookup và conversation bridge vẫn trả 503 cho tới khi module chủ quản đăng ký adapter.

## Discovery và Matching

Các route riêng tư yêu cầu policy `User`; UserId luôn lấy từ claim của JWT.

| Phương thức | Đường dẫn | Hợp đồng |
|---|---|---|
| GET | `/api/v1/discover?page=1&pageSize=20&minAge=18&maxAge=35&city=...&interest=...` | Chỉ trả hồ sơ hoạt động, email đã xác nhận, đủ 18 tuổi, đủ hồ sơ và đang bật khám phá. Không trả ngày sinh. Loại chính mình, Like/Pass đã gửi, Match đã từng tạo và block hai chiều. |
| POST | `/api/v1/user-likes/{targetUserId}` | Ghi Like lặp an toàn. Like hai chiều tạo tối đa một Match và một Conversation; phát sự kiện sau commit. Trả 503 nếu block hoặc chat adapter chưa sẵn sàng. |
| POST | `/api/v1/passes/{targetUserId}` | Ghi Pass idempotent cho hồ sơ đủ điều kiện; không tạo Match. |
| GET | `/api/v1/matches` | Trả Match đang hoạt động cùng ProfileSummary còn công khai và conversation UUID. |
| DELETE | `/api/v1/matches/{matchId}` | Chỉ thành viên mới hủy; lưu `UnmatchedAt`, xóa Like liên quan và yêu cầu Chat thu hồi quyền gửi. P0 không cho ghép lại cặp đã hủy. |

Lỗi lọc là 400; chưa đăng nhập là 401; hồ sơ chưa hoàn thiện hoặc cặp đã hủy là 409; hồ sơ ẩn/không tồn tại là 404; adapter/database chưa sẵn sàng là 503. Migration `AddDiscoveryMatching` phải được review và áp dụng theo lịch migration chung trước khi chạy các route cần DB.

Quy ước tạo kết nối cho TV4: mỗi Match đang hoạt động có đúng một Conversation; `EnsureConversationAsync(matchId)` là idempotent và dùng cùng scoped `AppDbContext`/transaction với Match. Sự kiện `MatchCreated` chỉ phát sau commit; nếu phát sự kiện thất bại, lần đọc danh sách Match là cách phục hồi. Khi bỏ Match, backend lưu `UnmatchedAt` trước khi yêu cầu Chat thu hồi quyền gửi tin. Đây là hợp đồng triển khai cần TV4 và TV3 xác nhận trước khi tích hợp adapter.

Quy ước: camelCase cho JSON, UTC cho thời gian, UUID cho định danh; DTO không trả
password hash, token bí mật hoặc ngày sinh của người khác. API nghiệp vụ sẽ dùng
Problem Details cho lỗi; health readiness là ngoại lệ có body health riêng.


## Tuần 2 — Block (#21)

GET/POST/DELETE `/api/v1/blocks`: JWT + CurrentUser Guid theo hợp đồng chung.
Chi tiết DTO, status, quy tắc hai chiều và ví dụ ghép module: [ISSUE_21_HANDOFF.md](ISSUE_21_HANDOFF.md).
