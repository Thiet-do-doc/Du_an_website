# Bản tích hợp nhóm — 26/10/2026

Tài liệu này là hướng dẫn hiện hành cho bản ghép vào main. Các tài liệu TV4 và
TICH_HOP_NHANH_THANH_VIEN mô tả lịch sử bàn giao; không áp lại patch/InitialTV4 từ đó.

## Nguồn đóng góp

- Nền main: e381eb0 (Auth Thiết, Profiles/ảnh của Võ qua PR #38).
- Võ: PR #39, ce1156f — hợp đồng hồ sơ riêng tư/công khai và tests.
- Thiện: PR #40, 8fdbe87 — Discovery, Like/Pass/Match và tests.
- Khuyến: PR #1, e880fe2 — Block, Report, Moderation và khung vận hành.
- Mạnh: nhánh feat/tv4-chat-notifications, f96916e — Chat/Notifications.

Lịch sử commit của các nhánh được giữ bằng merge. Các bản Auth độc lập, demo
database và migration tạo bảng Chat trùng không được đưa vào schema chung.

## Phần đã nối và sửa

- Một ProfileSummary chung; giữ DTO riêng tư/công khai và cách cập nhật sở thích
  theo chênh lệch của Võ. Discovery dùng Profiles thật và lọc Block hai chiều.
- Like hai chiều tạo Match + Conversation trong cùng transaction; thêm FK
  Conversation.MatchId. Sửa retry khi PostgreSQL tự rollback tại COMMIT và sửa
  offset phân trang khi lấy thêm một hồ sơ để tính hasMore.
- Chat dùng Match/Block thật, AuthProvider, JWT và AccountHubFilter chung; Hub
  đóng khi access token hết hạn. Client mở đúng conversation từ danh sách match.
- Report Message kiểm tra đối tượng bằng IChatReportTargetAccess, chỉ cho thành
  viên báo cáo tin của người kia; vẫn cho báo cáo lịch sử sau block/unmatch.
- Admin khóa tài khoản qua IAccountAccess; audit, trạng thái và thu hồi refresh
  cùng transaction, ngắt Hub sau commit. Không dùng AccountStatusService cũ.
- API hồ sơ công khai và ảnh yêu cầu đăng nhập, kiểm tra block hai chiều. Ảnh
  frontend tải bằng Bearer và Blob; không đưa JWT vào URL. Chủ ảnh xem được ảnh
  của mình khi tắt khám phá. /safety nối báo cáo/chặn; chỉ Admin thấy xử lý báo cáo.

## Chạy sau khi pull

Yêu cầu: .NET SDK 10 stable, Node 24, PostgreSQL 17; secrets local theo README.
Chỉ chạy một lượt migration của nhóm tại một thời điểm.

```powershell
dotnet tool restore
dotnet restore DatingApp.slnx
dotnet build DatingApp.slnx --configuration Release
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run build
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef migrations script --idempotent --project backend/DatingApp.Api --output .artifacts/migration-review.sql
# Kiểm tra connection string, sao lưu DB có dữ liệu cần giữ, review SQL rồi:
dotnet ef database update --project backend/DatingApp.Api
dotnet run --project backend/DatingApp.Api --launch-profile http
# Terminal khác:
npm.cmd --prefix frontend run dev
```

Thứ tự schema: InitialIdentity → AuthSessions → AddProfiles →
SafetyBlocksAndModeration → AddDiscoveryMatching → IntegrateChatWithMatching.
Migration cuối chỉ thêm bảng Chat/Notifications và FK thật; không tạo lại Identity.
Đã thử chuỗi từ DB trống và nâng từ schema AddProfiles trên hai DB test riêng.
Database datingapp đang dùng trên máy không bị sửa bởi các lần thử đó.

Nếu DB từng áp InitialTV4 hoặc AddChatNotifications của nhánh riêng, **không áp
thẳng chuỗi này**. Giữ bản sao lưu và dùng DB tích hợp mới; việc chuyển dữ liệu
demo/nhánh cũ cần kế hoạch riêng. Không xóa lịch sử migration của DB chung.

## Kiểm tra

Kết quả local ngày 26/10/2026: Release build không lỗi/cảnh báo; backend 53/53
tests đạt, không skipped; frontend 8/8 tests đạt và production build thành công.
Repository layout và EF model không có chênh lệch. Preview trình duyệt đã kiểm tra
đăng nhập, hồ sơ, khám phá, mở conversation từ match, kết nối SignalR, lịch sử,
gửi tin và trang an toàn với tài khoản User. Dữ liệu preview là tài khoản giả.

```powershell
# AUTH_TEST_CONNECTION phải trỏ database test riêng đã migrate.
dotnet test DatingApp.slnx --configuration Release
npm.cmd --prefix frontend test
./scripts/Test-RepositoryLayout.ps1
dotnet ef migrations has-pending-model-changes --project backend/DatingApp.Api
```

IntegratedModuleTests chạy API thật qua test host và PostgreSQL: Profiles,
phân trang, Match/Conversation, idempotent messages, quyền A/B/C, block hai chiều,
quyền hồ sơ/ảnh, báo cáo tin, unmatch và admin khóa/thu hồi/audit. CI chạy cả bộ
test với PostgreSQL 17; thiếu AUTH_TEST_CONNECTION làm test DB bị skipped.

Smoke SignalR dùng frontend/scripts/tv4-smoke.mjs và tài khoản giả đã xác nhận.
Thiết lập TV4_EMAIL_A/B/C và TV4_PASSWORD_A/B/C trong môi trường, không commit.
A/B cần match thật. TV4_TEST_ALLOW_RELATIONSHIP_MUTATION=true bật thử block;
TV4_TEST_ALLOW_UNMATCH=true chỉ dùng với cặp thử bỏ được. Script tải 20 kết nối
là phép đo local; không suy ra khả năng tải production.

## Giới hạn

Chưa triển khai production/SMTP thật, chưa kiểm chứng backup/restore hoặc vận hành
nhiều backend. Posts chưa thuộc bản tích hợp. Việc ghép code không thay thế review
của thành viên khác, bài thực hành chéo và nghiệm thu tuần theo từng issue.
