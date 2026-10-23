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

`IDiscoveryProfileQuery` cung cấp hồ sơ công khai, kiểm tra độ hoàn thiện, tìm
kiếm theo độ tuổi/thành phố/sở thích và tải theo danh sách UUID cho Discovery.
Truy vấn loại tài khoản khóa/chưa xác nhận, hồ sơ ẩn/chưa hoàn thiện và không
trả ngày sinh. Hồ sơ được xem là hoàn thiện khi tài khoản hoạt động, đã xác nhận
email, đủ 18 tuổi, có tên hiển thị và ít nhất một sở thích. Ảnh là tùy chọn;
giao diện Discovery có thể dùng avatar mặc định khi hồ sơ chưa có ảnh.

Không trả email hoặc ngày sinh đầy đủ trong DTO hồ sơ. Migration được tạo nhưng
chưa áp dụng lên cơ sở dữ liệu; hãy review SQL và phối hợp thứ tự với TV1 trước
khi cập nhật DB tích hợp.
