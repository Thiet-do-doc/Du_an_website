# DiscoveryMatching frontend
Chủ trì TV3; người dự phòng TV4.

## Giao diện và API

- `pages/DiscoveryPage.tsx`: màn hình khám phá, lọc tuổi/khu vực/sở thích, phân trang, thích/bỏ qua, danh sách match và bỏ match; có loading/error/empty state.
- `api/discovery.ts`: gọi API `/api/v1/discover`, `/user-likes`, `/passes`, `/matches` qua `authenticatedFetch`, gồm bearer access token và refresh phiên.
- `types/discovery.ts`: kiểu dữ liệu hồ sơ, bộ lọc, phân trang, kết quả like và match.

Frontend không tự tạo match giả. Route `/discover` yêu cầu phiên đăng nhập; backend kiểm tra hồ sơ thật. Khi Block/Chat adapter chưa được đăng ký, backend trả 503 theo cơ chế fail-closed.

## Phụ thuộc còn phải nối

- TV1: đã có JWT và `authenticatedFetch`; API lấy UserId từ claim của phiên, không nhận ID từ client.
- TV2: `ProfileService` cung cấp tìm kiếm hồ sơ hoạt động, đã xác nhận, đủ 18 tuổi, hoàn thiện và đang hiển thị; phản hồi không chứa ngày sinh.
- TV5: cần đăng ký `IBlockLookup` hai chiều. Adapter mặc định từ chối truy vấn để fail-closed.
- TV4: cần đăng ký `IMatchConversationBridge`; `EnsureConversationAsync` phải idempotent, trả conversation UUID và cùng scoped `AppDbContext`/transaction. Sự kiện match phát sau commit; client tải lại danh sách để khôi phục nếu lỡ sự kiện.
- Migration `AddDiscoveryMatching` thêm ba bảng cùng FK tới UUID của Identity; chưa áp dụng vào database.
- P1 Posts chỉ bắt đầu sau khi P0 ổn định và TV2 cung cấp module Posts.

`/discover` nằm trong router chung và yêu cầu đăng nhập. Hủy Match giữ lịch sử và cấm ghép lại P0; quyền gửi tin phải kiểm tra trạng thái Match ở Chat.
