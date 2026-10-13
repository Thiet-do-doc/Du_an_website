# DiscoveryMatching frontend
Chủ trì TV3; người dự phòng TV4.

## Bản xem trước hiện tại

- `pages/DiscoveryPage.tsx`: màn hình khám phá, lọc tuổi/khu vực/sở thích, thích hoặc bỏ qua hồ sơ, và danh sách kết nối.
- `api/demoProfiles.ts`: dữ liệu trình bày tạm; một số hồ sơ tạo match minh hoạ khi được thích.
- `types/discovery.ts`: kiểu dữ liệu frontend dự kiến cho hồ sơ, bộ lọc và match.

Toàn bộ thao tác hiện lưu trong React state và mất khi tải lại. Đây chưa phải luồng người dùng thật, chưa gửi yêu cầu lên backend và chưa tạo conversation. Không dùng dữ liệu mẫu cho demo phân quyền hay bảo mật.

## Tích hợp tiếp theo

Sau khi TV1 có đăng nhập và TV2 cung cấp `ProfileSummary`, thay adapter dữ liệu mẫu bằng API đã xác thực:

- `GET /api/v1/discover` — lọc và phân trang hồ sơ, loại chính mình, hồ sơ ẩn/khóa và block hai chiều.
- `POST /api/v1/user-likes/{targetUserId}` và `POST /api/v1/passes/{targetUserId}`.
- `GET /api/v1/matches` và `DELETE /api/v1/matches/{id}`.

Khi backend báo match mới, TV3/TV4 cần bảo đảm conversation chỉ được tạo một lần và sự kiện realtime chỉ phát sau commit. Bỏ match phải thu hồi quyền gửi tin ở backend.

- `pages/`: màn hình cấp route.
- `components/`: thành phần chỉ dùng trong module.
- `api/`: adapter dữ liệu; tái sử dụng client ở `shared/api/` khi API chung được bổ sung.
- `types/`: kiểu dữ liệu frontend; cần đồng bộ thành DTO API sau khi chốt hợp đồng.

Route preview được mở từ thẻ TV3 trên trang khung. Auth, route sản phẩm, API thật và backend chưa có trong repository hiện tại.
