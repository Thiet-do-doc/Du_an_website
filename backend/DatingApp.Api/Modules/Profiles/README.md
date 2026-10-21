# Profiles

Phụ trách TV2. Module dùng `ApplicationUser<Guid>` và Auth hiện có. Migration
`AddProfiles` thêm `Profiles`, `Photos`, `Interests`, `UserInterests` cùng danh
sách sở thích khởi tạo.

## API

- `GET /api/v1/profiles/me` — hồ sơ người dùng đang đăng nhập.
- `PUT /api/v1/profiles/me` — tạo/cập nhật hồ sơ và danh sách sở thích. Chỉ sửa
  hồ sơ từ user ID trong `ICurrentUser`.
- `GET /api/v1/profiles/{id}` — thông tin công khai của hồ sơ đang hoạt động,
  đã xác nhận email và bật khám phá.
- `GET /api/v1/interests` — danh sách sở thích.
- `POST /api/v1/photos` — multipart field `file`, tối đa 5 MB/ảnh và 6 ảnh/user.
- `PUT /api/v1/photos/{id}/avatar`, `PUT /api/v1/photos/order`,
  `DELETE /api/v1/photos/{id}` — chọn ảnh đại diện, sắp xếp và xóa ảnh của chính
  user hiện tại.
- `GET /api/v1/photos/{id}/content` — nội dung ảnh của hồ sơ công khai.

Ảnh được kiểm tra bằng ImageMagick, giới hạn định dạng JPEG/PNG/WebP và kích
thước giải mã; ảnh được xoay theo hướng EXIF, xóa metadata và mã hóa lại trước
khi lưu trong PostgreSQL (`bytea`). Không phụ thuộc thư mục local hay dịch vụ ảnh
bên ngoài. Khi triển khai tải ảnh lớn, nhóm có thể chuyển nội dung sang object
storage qua adapter riêng.

`IDiscoveryProfileQuery` cung cấp projection
`DiscoveryMatching.DTOs.ProfileSummary` theo hợp đồng của Thiện. Hồ sơ đủ điều
kiện khám phá khi tài khoản hoạt động, email đã xác nhận, đủ 18 tuổi, có tên
hiển thị và ít nhất một sở thích; hồ sơ cũng phải bật khám phá. Ảnh không phải
điều kiện hoàn thiện trong tuần 2 (upload ảnh thuộc T3-NV2). Tuổi được tính từ
ngày sinh, ngày sinh chính xác và email không được đưa vào projection.

API của chủ hồ sơ dùng `MyProfileResponse`; API xem công khai dùng
`PublicProfileResponse`. Hai hợp đồng không chứa email hay ngày sinh chính xác,
nhưng chỉ hợp đồng riêng tư có cờ bật khám phá và trạng thái hoàn thiện.

Test tích hợp PostgreSQL tạo tài khoản và hồ sơ giả riêng cho lần chạy. Không
seed hồ sơ người dùng vào migration hoặc môi trường production. Dùng tuổi, thành
phố và danh sách sở thích khác nhau để kiểm tra bộ lọc discovery.

Không trả email hoặc ngày sinh đầy đủ trong DTO hồ sơ. Migration được tạo nhưng
chưa áp dụng lên cơ sở dữ liệu; hãy review SQL và phối hợp thứ tự với TV1 trước
khi cập nhật DB tích hợp.
