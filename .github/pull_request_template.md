## Issue và mục tiêu
- Issue:
- Người review:

## Thay đổi
-

## Cách kiểm tra
- Lệnh hoặc thao tác:
- Kết quả:
- Đã thử dữ liệu sai và sai quyền:

## Ảnh hưởng tới người khác
- DTO/schema/migration/biến môi trường thay đổi:
- Module cần cập nhật:

- [ ] Không có secret, token hoặc dữ liệu người thật
- [ ] Build và test qua
- [ ] Đã cập nhật tài liệu liên quan
- [ ] Nhánh dựa trên main có nền Auth Thiết; không thêm project/Auth trùng ở gốc
- [ ] Dùng UUID và JWT chung; giữ các service/AuthProvider đang có
- [ ] Nếu đổi DB: ghi migration đã áp dụng ở đâu, test DB riêng, không tạo lại bảng Identity
- [ ] Nếu sửa file chung: mô tả phần thêm, không ghi đè toàn bộ bằng bản nhánh cũ
