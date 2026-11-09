# Báo cáo T4-NV3 — Issue #29

## Đã triển khai và tích hợp

- `MatchChatBridge` là implementation thật của `IMatchConversationBridge`, được đăng ký trong DI cùng `IntegratedChatMatchAccess` và block policy thật.
- Tạo match và conversation trong cùng transaction/scoped `AppDbContext`; unique index trên `Conversations.MatchId` bảo đảm tối đa một conversation cho mỗi match. Notification và sự kiện `MatchCreated` được phát sau commit.
- Danh sách match trả conversation ID thật; UI mở `/chat?conversation={id}`. API conversations trả hội thoại thật từ database.
- Unmatch giữ lịch sử. `ChatPermissions` cho phép đọc sau unmatch khi tài khoản còn hoạt động và không bị block, đồng thời chặn gửi tin. Quyền được kiểm tra lại ở service khi gửi qua SignalR.
- Block hai chiều loại đối tượng khỏi discovery/matches và chặn chat. Unblock không khôi phục match đã unmatch; tải lại danh sách là cách đồng bộ nếu bỏ lỡ realtime event.
- Mở rộng `IntegratedModuleTests` để gửi like hai chiều đồng thời qua API thật, xác nhận một match/một conversation, kiểm tra conversation ID trong match list, discovery sau block, lịch sử sau unmatch và gửi tin bị từ chối từ cả hai phía.
- Cập nhật README của DiscoveryMatching và ChatNotifications theo trạng thái tích hợp thật.

## Kiểm tra

- `dotnet build tests/DatingApp.Api.Tests/DatingApp.Api.Tests.csproj --configuration Release --no-restore` — đạt, 0 warning/error.
- `npm ci` — đạt; `npm run build` — đạt.
- Chưa chạy test PostgreSQL: môi trường hiện không có `AUTH_TEST_CONNECTION` và không có Docker để dựng database test. Test runner local cũng thoát trước khi xuất kết quả test; cần chạy integration suite trên CI hoặc máy có PostgreSQL test.
- Chưa có kết quả CI/review từ Mạnh/Khuyến và chưa demo; cần đính kèm kết quả CI/review vào issue/PR trước khi đánh dấu hoàn tất chung.
