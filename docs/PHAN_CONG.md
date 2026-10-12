# Phân công vào bộ khung

Mỗi người làm xuyên suốt React → API C# → database → test của module mình.

| Thành viên | Backend module | Frontend feature | Dự phòng |
|---|---|---|---|
| TV1 | Auth | auth | TV2 |
| TV2 | Profiles | profiles | TV3 |
| TV3 | DiscoveryMatching | discovery-matching | TV4 |
| TV4 | ChatNotifications | chat-notifications | TV5 |
| TV5 | SafetyAdmin | safety-admin | TV1 |

Posts do TV2 + TV3 làm sau khi P0 ổn. TV5 hướng dẫn vận hành, cả nhóm cùng deploy.

## Đầu việc tuần 1
1. Mọi người clone, chạy frontend/API và tạo PR nhỏ.
2. TV1 chốt UserId UUID, Identity, phiên đăng nhập, DTO lỗi và quyền.
3. TV2 chốt ProfileSummary, UI form dùng chung và quy tắc ảnh.
4. TV3 chốt like/match và hợp đồng tạo conversation cùng TV4.
5. TV4 chốt message DTO, clientMessageId, sự kiện và quyền tham gia chat.
6. TV5 chốt BlockPolicy, report status và giúp chạy thử theo README.

## Quy tắc chung
- Chưa viết năm schema riêng rồi ghép cuối kỳ. Dùng AppDbContext chung ban đầu.
- Chủ module thêm Controllers/DTOs/Services/Entities ngay trong module.
- Chỉ một người tạo migration tại một thời điểm, pull main trước khi tạo.
- Không tạo migration rỗng; hiện bộ khung chưa có bảng nghiệp vụ hoặc migration.
- Chỉ gửi tin khi match hợp lệ; block/quyền phải được kiểm tra ở backend.
- Mỗi PR có một người khác review. Mỗi tuần đổi người cầm bàn phím khi làm cặp.
- Người dự phòng phải tự sửa ít nhất một lỗi ở module được giao dự phòng.
