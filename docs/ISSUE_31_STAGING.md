# Tuần 4 · Khuyến · Admin audit và staging (#31)

**Trạng thái bàn giao:** backend và frontend đã kiểm thử cục bộ trên nền `main@63d9226`. Bước triển khai đang chờ host, URL HTTPS và quyền vận hành do nhóm cấp. Thiết review PR; Khuyến và chủ từng module ghi kết quả chạy trên cùng một commit trước khi đóng [issue #31](https://github.com/Thiet-do-doc/Du_an_website/issues/31).

## API và màn hình quản trị

Đăng nhập bằng tài khoản Admin, vào **Tài khoản → Quản lý báo cáo** hoặc mở trực tiếp `/admin`. Người dùng gửi báo cáo ở `/safety`. Backend kiểm tra policy Admin ở mọi API bên dưới. Các đường dẫn tương đối với `/api/v1/admin`.

| API | Dữ liệu và kết quả |
| --- | --- |
| GET /reports?status=Pending&page=1&pageSize=25 | Lọc theo trạng thái, bỏ status để xem tất cả. |
| GET /reports/{id} | Chi tiết mô tả, loại và ID đối tượng, người gửi, kết quả xử lý. |
| POST /reports/{id}/evidence | Với báo cáo tin nhắn, trả đúng `{id, content}` của tin được báo cáo và ghi sự kiện xem. Loại khác hoặc sai ID trả 404. |
| GET /reports/{id}/actions | Nhật ký riêng của báo cáo, gồm người xử lý, lý do, tài khoản đích, thời điểm UTC và thao tác. |
| PATCH /reports/{id} | Gửi `{"status":"Actioned","notes":"Lý do quyết định"}`. Có thể xem xét, bác bỏ hoặc đánh dấu đã xử lý. Hai trạng thái cuối là Dismissed và Actioned. Quyết định xung đột trả 409. |
| PATCH /users/{id}/status | Gửi `{"isActive":false,"reason":"Lý do khóa","reportId":"<UUID>"}`, dùng true để mở. Tài khoản phải khớp đối tượng của báo cáo. Client cũ vẫn có thể gọi API quản trị độc lập và bỏ reportId. |

Khóa/mở dùng `IAccountAccess` của Auth, thay đổi trạng thái, thu hồi phiên và ghi audit trong cùng transaction. Sau commit, các kết nối Hub của tài khoản bị ngắt. Người được mở khóa phải đăng nhập lại. Nhật ký cũ chỉ xuất hiện dưới báo cáo khi đã có ReportId. Bản này dùng các bảng Reports và ModerationActions hiện hành.

## Triển khai trên host Linux đã cấp quyền

1. Chuẩn bị Docker với Compose v2, Node 24 để chạy smoke, tên miền trỏ về host, chứng chỉ TLS hợp lệ và tài khoản SMTP thử. Dùng project name cố định để lần cập nhật sau nhận đúng volume. Với staging đang vận hành, dùng **đúng project name hiện tại**, sao lưu trước migration và hẹn khung bảo trì với nhóm.
2. Checkout đúng commit đã review. Ghi SHA, tạo `.env` từ `.env.example` cho lần cài mới, giới hạn quyền đọc file. Điền DB, JWT key ngẫu nhiên ít nhất 32 byte, URL frontend HTTPS, địa chỉ gửi và thông số SMTP. Dùng mật khẩu DB sinh ngẫu nhiên dạng hex để tương thích connection string. File này giữ riêng trên host. Nếu đã có volume, dùng thông tin xác thực hiện hành.
3. Từ gốc repo chạy các lệnh dưới đây trong shell của người triển khai.

```sh
git rev-parse HEAD
export COMPOSE_PROJECT_NAME=mylove-staging
export COMPOSE_FILE=compose.staging.yml
docker compose config --quiet
docker compose build api web
docker compose up -d --wait postgres
docker network inspect "${COMPOSE_PROJECT_NAME}_internal" --format '{{(index .IPAM.Config 0).Subnet}}'
```

4. Đặt `TRUSTED_PROXY_CIDR` trong `.env` bằng subnet vừa in. Chạy migration một lần từ image của chính commit đó. Chỉ tiếp tục khi lệnh trả mã 0.

```sh
docker compose run --rm --no-deps api --migrate=true
docker compose up -d --wait --force-recreate --no-deps api web
docker compose ps
```

5. Dùng `infra/staging-edge.nginx.conf.example` cho Nginx trên host. Thay cả hai tên miền, đường dẫn chứng chỉ và cổng loopback theo `STAGING_PORT`. Kiểm tra `nginx -t` trước khi reload. Với proxy TLS do nhóm quản lý, áp dụng cùng quy tắc Host, X-Forwarded-Proto và WebSocket Upgrade. Chỉ web bind vào loopback, PostgreSQL và API dùng mạng Docker. Kiểm tra cột Ports và các rule firewall của host.
6. Đăng ký và xác nhận email tài khoản thử C, sau đó người vận hành chạy `docker compose run --rm --no-deps api --grant-admin="<email thử C>"`. A và B giữ quyền thành viên. Dùng ba hồ sơ trình duyệt riêng.

## Nghiệm thu trên staging

| Người kiểm tra | Thao tác | Kết quả cần ghi |
| --- | --- | --- |
| Khuyến | `node scripts/staging-smoke.mjs https://<host>`; mở trực tiếp và tải lại /admin | HTTPS hợp lệ, bundle JS được phục vụ, API/DB healthy, cookie CSRF Secure, admin ẩn danh 401. |
| Thiết, Võ | A/B đăng ký, nhận mail, xác minh, đăng nhập, tạo hồ sơ có ảnh và bật khám phá | SMTP gửi đến hộp thư thử, hồ sơ và ảnh còn sau tải lại. |
| Thiện, Mạnh | A/B tìm và like nhau; vào chat, gửi hai tin thử. B báo cáo một tin của A. Mở DevTools Network → WS | Hai phía thấy tin; đường /hubs/chat qua HTTPS nâng cấp 101; hiển thị lại lịch sử sau tải lại. |
| Khuyến, Thiết | B mở /admin và gọi GET /api/v1/admin/reports bằng token thành viên. C mở báo cáo và bấm xem bằng chứng | B nhận 403. C chỉ thấy tin đã báo cáo, thao tác đọc có người xem và giờ UTC. |
| Khuyến | C khóa A từ báo cáo. A thử dùng phiên cũ để tải dữ liệu/gửi tin; C mở lại và đánh dấu Đã xử lý | Phiên và Hub cũ bị thu hồi. A đăng nhập lại được. Lọc Đã xử lý để xem đúng lý do và thứ tự các thao tác. |
| Khuyến | Ghi ID report thử; trong khung kiểm tra chạy `docker compose restart postgres api web`, đợi healthy, chạy lại smoke và mở report | Cùng ID, quyết định, lịch sử và hồ sơ còn đủ. |

Nếu deploy lỗi, giữ volume, xem trạng thái container và log lỗi đã che dữ liệu nhạy cảm. Nếu migration chưa áp dụng, có thể build lại commit trước. Nếu schema đã đổi, đối chiếu tính tương thích với Thiết trước khi hạ bản ứng dụng hoặc khôi phục backup.

## Bằng chứng để bàn giao

Kiểm thử cục bộ trên PostgreSQL 18.4 riêng, chạy migration từ DB trống: build Release thành công, 59/59 test backend đạt, React production build thành công, 8/8 test frontend đạt. Bài `RealModulesConnectProfilesMatchChatBlockReportAndAccountRevocation` kiểm tra quyền, bằng chứng đúng tin nhắn, khóa/mở tài khoản, nhật ký và hai yêu cầu xử lý đồng thời. CI của repo dùng PostgreSQL 17.

`docker compose config` đã kiểm tra cấu hình cục bộ: chỉ web publish lên loopback, API và PostgreSQL dùng mạng riêng, volume DB và health dependency đúng. Job CI `staging-compose` sẽ build image, migrate, kiểm tra HTTPS và đọc lại dữ liệu sau khi tạo lại container trên runner của PR.

Khi có host, bổ sung vào PR: SHA từ `git rev-parse HEAD`, URL, ngày giờ chạy, tên người xác nhận từng dòng, kết quả PASS/FAIL, lỗi còn lại và người phụ trách. Đính kèm ảnh màn hình đã che email/ID hoặc kết quả smoke. Hiện còn chờ xác nhận HTTPS, WebSocket 101, dữ liệu sau restart, CI của PR và review trên bản triển khai chung.
