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
Auth, Profiles và Chat chưa hoàn tất; DiscoveryMatching trả 401 khi không có principal đã xác thực
và 503 khi adapter hoặc database phụ thuộc chưa được cấu hình.

## Hợp đồng DiscoveryMatching đã chốt cho tích hợp

Các route dưới đây đã được map trong API TV3. Để dùng luồng đã đăng nhập, TV1 cần cấu hình middleware xác thực, TV2 cần đăng ký truy vấn hồ sơ, TV5 cần đăng ký block lookup và TV4 cần cài `IMatchConversationBridge`. Nếu thiếu database hoặc adapter, API từ chối đóng (401/503); backend luôn lấy UserId từ principal, client không được gửi FromUserId để quyết định quyền.

| Phương thức | Đường dẫn | Hợp đồng / quy tắc |
|---|---|---|
| GET | `/api/v1/discover?page=1&pageSize=20&minAge=18&maxAge=35&city=...&interest=...` | Chỉ trả ProfileSummary; pageSize mặc định 20, tối đa 50. Loại chính mình, hồ sơ thiếu/ẩn/khóa và block cả hai chiều. Không trả ngày sinh. |
| POST | `/api/v1/user-likes/{targetUserId}` | 200 `{isMatch, matchId, conversationId, matchedAt}`. Một chiều chỉ ghi like; hai chiều tạo hoặc khôi phục một match và bảo đảm một conversation. Từ chối tự like, block và hồ sơ không đủ điều kiện. |
| POST | `/api/v1/passes/{targetUserId}` | 204; ghi pass lặp an toàn, không tạo match. |
| GET | `/api/v1/matches` | 200 danh sách match active, sắp mới nhất trước, gồm `id`, `otherUserId`, `conversationId`, `matchedAt`, `isActive`, `otherProfile`. Block và hồ sơ không còn hiển thị sẽ bị loại khỏi kết quả. |
| DELETE | `/api/v1/matches/{id}` | 204 khi người gọi là thành viên; quyền gửi tin bị thu hồi ngay từ trạng thái match. 404 với match không thuộc người gọi. |

Request không hợp lệ dùng Problem Details; bộ lọc sai là 400, chưa đăng nhập 401, tài nguyên không tồn tại/không được hiển thị là 404, dependency chưa sẵn sàng là 503. Sự kiện realtime chỉ phát sau commit; client luôn có thể tải lại `/matches` để đồng bộ. Hợp đồng ensure-conversation với TV4 là `EnsureConversationAsync(matchId)` trả conversation UUID, idempotent và dùng chung transaction EF; việc phát thông báo là sau commit.

Migration `20261015150402_DiscoveryMatchingInitial` tạo các bảng `UserLikes`, `Passes`, `Matches` cùng khóa/constraint/index. Migration không chạy tự động khi API khởi động; cần áp dụng theo lịch migration chung và bổ sung foreign key sau khi schema người dùng của TV1 được chốt.

Quy ước: camelCase cho JSON, UTC cho thời gian, UUID cho định danh; DTO không trả
password hash, token bí mật hoặc ngày sinh của người khác. API nghiệp vụ sẽ dùng
Problem Details cho lỗi; health readiness là ngoại lệ có body health riêng.
