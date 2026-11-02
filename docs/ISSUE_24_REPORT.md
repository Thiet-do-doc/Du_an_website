# Báo cáo T3-NV3 — Issue #24

## Phạm vi đã hoàn tất trong TV3

- API like/pass lấy người dùng hiện tại từ JWT; không nhận ID người gửi từ client.
- Controller kiểm tra hồ sơ đã hoàn thiện, tài khoản/đối tượng có thể truy vấn và block trước khi chuyển yêu cầu vào service. Service từ chối tự like/tự pass; database có check constraint chống tự tương tác.
- `UserLikes` và `Passes` có khóa ghép theo cặp người dùng. Match lưu cặp UUID theo thứ tự chuẩn hóa, có unique constraint và trạng thái active/unmatched.
- Luồng like dùng transaction PostgreSQL `Serializable`. Lỗi serialization, deadlock hoặc unique conflict được thử lại có backoff; like lặp trả về cùng match thay vì tạo match mới.
- Giao diện Khám phá/Kết nối gọi API thật, có thao tác like/pass, danh sách match và bỏ match.
- Migration `AddDiscoveryMatching` đã có trong lịch sử migration; không tạo migration trùng.
- Bổ sung test tích hợp PostgreSQL gửi đồng thời 8 like cùng chiều và xác nhận chỉ có một lượt like; test hiện có bao phủ like hai chiều đồng thời, match duy nhất, tự like, quyền bỏ match và không ghép lại sau khi bỏ match.

## Kiểm tra trong workspace

- Frontend: `npm.cmd run build` — đạt.
- Backend/test project: test project biên dịch được trong lệnh `dotnet test`.
- Test runner không trả kết quả hoàn tất trong môi trường hiện tại; `AUTH_TEST_CONNECTION` chưa được cấu hình nên test PostgreSQL chưa được thực thi. Cần chạy lại test matching trên PostgreSQL test trước khi gắn kết quả vào issue/PR.
- CI, review của Mạnh/Thiết và PR chưa được thực hiện từ workspace này.

## Phụ thuộc TV4 còn chờ

`IMatchConversationBridge` là contract của TV3. Trong checkout hiện tại, DI vẫn đăng ký `UnconfiguredMatchConversationBridge`, và module ChatNotifications chưa có implementation thật. Vì vậy chưa thể demo tạo conversation/chat thật; không dùng stub làm bằng chứng tích hợp. TV4 cần nối adapter thật theo #29, dùng chung `AppDbContext`/transaction như contract và phát realtime sau commit.

## Nội dung đề xuất cho Issue #24

> TV3 matching đã triển khai API like/pass, kiểm tra quyền và điều kiện hồ sơ/block, lưu like/pass bằng khóa cặp, tạo match hai chiều trong transaction `Serializable` với cặp chuẩn hóa và unique constraint. UI Khám phá/Kết nối dùng API thật. Đã bổ sung test PostgreSQL cho like lặp đồng thời; migration matching đã tồn tại nên không tạo migration mới.
>
> Frontend build đạt và backend/test project biên dịch được. Chưa xác nhận test PostgreSQL do môi trường chưa có `AUTH_TEST_CONNECTION`; cần chạy trên PostgreSQL test và đính kèm kết quả. Contract conversation đã có nhưng implementation TV4 cho #29 chưa được nối trong checkout, nên phần demo chat/conversation chờ TV4. Chưa có CI, reviewer hoặc PR được ghi nhận.
