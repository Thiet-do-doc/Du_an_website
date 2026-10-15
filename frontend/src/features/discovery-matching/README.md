# DiscoveryMatching frontend
Chủ trì TV3; người dự phòng TV4.

## Giao diện và API

- `pages/DiscoveryPage.tsx`: màn hình khám phá, lọc tuổi/khu vực/sở thích, phân trang, thích/bỏ qua, danh sách match và bỏ match; có loading/error/empty state.
- `api/discovery.ts`: gọi API `/api/v1/discover`, `/user-likes`, `/passes`, `/matches`; hỗ trợ cookie cùng origin và chuyển DTO API sang kiểu giao diện. Cần nối bearer access token qua HTTP client chung khi TV1 hoàn tất Auth.
- `types/discovery.ts`: kiểu dữ liệu hồ sơ, bộ lọc, phân trang, kết quả like và match.

Không còn tự tạo match giả trên frontend. API cần principal đã xác thực; bộ khung chưa cài Auth nên yêu cầu hiện trả 401 cho tới khi TV1 nối middleware. Khi profile/block/chat adapter chưa được đăng ký, backend trả 503 theo cơ chế fail-closed.

## Phụ thuộc còn phải nối

- TV1: cài authentication middleware/JWT và gắn bearer access token vào HTTP client chung. Controller chỉ chấp nhận UUID từ claim `NameIdentifier` hoặc `sub`; không có đường nhận UserId của người thực hiện từ client.
- TV2: đăng ký `IDiscoveryProfileQuery`; triển khai tìm hồ sơ đủ tuổi, đủ điều kiện, chưa ẩn/khóa; lọc tuổi bằng ngày sinh nhưng chỉ trả tuổi, cùng phân trang và truy vấn nhiều hồ sơ theo UUID.
- TV5: đăng ký `IBlockLookup` trả danh sách block hai chiều. Adapter mặc định từ chối truy vấn để fail-closed.
- TV4: đăng ký `IMatchConversationBridge`; `EnsureConversationAsync` phải idempotent, trả conversation UUID và cùng scoped `AppDbContext`/transaction. Sự kiện match phát sau commit; client tải lại danh sách để khôi phục nếu lỡ sự kiện.
- Migration `20261015150402_DiscoveryMatchingInitial` đã tạo ba bảng TV3; cần đồng bộ slot áp dụng với nhóm và bổ sung khóa ngoại sau khi schema Auth của TV1 sẵn sàng.
- P1 Posts chỉ bắt đầu sau khi P0 ổn định và TV2 cung cấp module Posts.

Route preview được mở từ thẻ TV3 trên trang khung. Bản khung hiện chưa có Auth, Profile, block hoặc chat adapter thật nên các request chưa đăng nhập trả 401 và các chức năng cần adapter trả 503 cho tới khi các module đó được nối.
