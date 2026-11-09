# ChatNotifications

- Chủ trì: **TV4**. Dự phòng: **TV5**.
- Phạm vi: hội thoại theo match thật, lịch sử tin nhắn, SignalR, chống gửi lặp và thông báo.
- Frontend: `frontend/src/features/chat-notifications/`.

## Tích hợp với Matching và Safety

- `MatchChatBridge` triển khai `IMatchConversationBridge` của TV3. `EnsureConversationAsync` dùng chung `AppDbContext`/transaction của `MatchService`, tạo một conversation cho mỗi match và trả lại conversation đã có khi retry. Notification và sự kiện `MatchCreated` chỉ phát sau khi match/conversation đã commit.
- Unique index `Conversations.MatchId` ngăn conversation trùng. Danh sách conversation/match đọc dữ liệu database thật; client lấy lại danh sách để đồng bộ nếu lỡ sự kiện realtime.
- `ChatPermissions` xác minh thành viên, cặp participant, trạng thái tài khoản, match và block ở cả hai chiều. Unmatch giữ conversation/tin nhắn để đọc lịch sử nhưng từ chối gửi tin mới; block ẩn và từ chối truy cập chat. Quyền được kiểm tra lại ngay trước khi lưu tin.
- SignalR và HTTP dùng chung quy tắc quyền. Không dựa vào trạng thái UI để quyết định quyền gửi.

## API và kiểm thử

- `GET /api/v1/conversations`, `GET /api/v1/conversations/{id}/messages`, API gửi tin qua SignalR và các route notifications yêu cầu tài khoản xác thực.
- Test nghiệp vụ nằm trong `TV4ChatRulesTests.cs`; `IntegratedModuleTests.cs` kiểm tra tích hợp trên PostgreSQL thật, gồm match, conversation, block, unmatch và quyền chat.
- Cần cấu hình `AUTH_TEST_CONNECTION` trỏ đến database test đã áp dụng migrations để chạy test tích hợp.
