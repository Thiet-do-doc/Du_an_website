# DiscoveryMatching

- Chủ trì: **TV3**. Dự phòng: **TV4**.
- Phạm vi: khám phá, bộ lọc, like hai chiều, match, danh sách match và unmatch.
- Frontend: `frontend/src/features/discovery-matching/`.

## Nghiệp vụ và tích hợp

- Người like/pass lấy từ principal đã xác thực; controller kiểm tra profile hoàn chỉnh, đối tượng đủ điều kiện và block hai chiều. Service và database đều chặn tự tương tác.
- `UserLikes`/`Passes` dùng khóa ghép theo cặp. `Matches` chuẩn hóa cặp UUID, có unique constraint; `UnmatchedAt` lưu trạng thái và lịch sử unmatch.
- Like hai chiều chạy trong transaction PostgreSQL `Serializable`. Lỗi serialization, deadlock hoặc unique conflict được retry có backoff. Request lặp trả match hiện có.
- `IMatchConversationBridge` do ChatNotifications triển khai bằng `MatchChatBridge`. Tạo conversation sử dụng cùng scoped `AppDbContext` và transaction với match; unique index bảo đảm tối đa một conversation cho mỗi match. Sự kiện/notification được phát sau commit.
- `GET /api/v1/matches` chỉ trả match đang hoạt động và conversation ID thật. `DELETE /api/v1/matches/{id}` đánh dấu unmatch; ChatPermissions cho đọc lịch sử nhưng từ chối gửi tin mới. Block ẩn đối tượng khỏi discovery/matches và chặn truy cập chat.
- Frontend dùng API thật; nút mở hội thoại điều hướng tới `/chat/{conversationId}`. Nếu lỡ sự kiện realtime, tải lại danh sách qua API để đồng bộ.

## Bảng và migration

Migration matching tạo `UserLikes`, `Passes`, `Matches`; migration tích hợp chat tạo `Conversations`, `Messages`, `Notifications`. Conversation có unique index trên `MatchId`. Không chạy migration tự động khi API khởi động; áp dụng theo thứ tự do người quản lý migration thống nhất.

## Kiểm thử

Test PostgreSQL trong `tests/DatingApp.Api.Tests/DiscoveryMatchingPostgresTests.cs` bao phủ like một chiều/hai chiều, request lặp và đồng thời, unique match, unmatch và block filtering. `IntegratedModuleTests.cs` kiểm thử luồng Auth/Profile/Match/Conversation/Block/Unmatch trên PostgreSQL thật. Cần cấu hình `AUTH_TEST_CONNECTION` trỏ đến database test đã áp dụng migration để chạy các test này.
