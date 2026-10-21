# Staging và vận hành

`compose.staging.yml` chạy frontend tĩnh qua Nginx, API .NET và PostgreSQL trên
mạng nội bộ Compose. Chỉ Nginx được publish ra host, mặc định `127.0.0.1:8080`.
Đặt reverse proxy có TLS phía trước nếu staging cần truy cập ngoài máy. Chưa bật
staging công khai trước khi xác minh Auth, quyền và apply migration nghiệp vụ.

## Chạy staging

Yêu cầu Docker Compose v2 và Docker build hỗ trợ .NET 10/Node 24. Tạo file `.env`
ở thư mục gốc từ mẫu dưới đây; dùng secret dev riêng, không commit file:

```dotenv
POSTGRES_DB=datingapp
POSTGRES_USER=datingapp
POSTGRES_PASSWORD=THAY_BANG_SECRET_LOCAL_DAI_NGAU_NHIEN
STAGING_PORT=8080
```

```powershell
docker compose --env-file .env -f compose.staging.yml config
docker compose --env-file .env -f compose.staging.yml up -d --build
docker compose --env-file .env -f compose.staging.yml ps
Invoke-WebRequest http://127.0.0.1:8080/healthz
```

`/healthz` chỉ xác nhận Nginx phục vụ được trang. API có healthcheck riêng; kiểm tra
chi tiết bằng `docker compose ... logs api`. Repository đã có migration SafetyAdmin;
đội triển khai cần apply migration có kiểm soát trước khi phục vụ người dùng. Không
đưa cổng database ra internet. `.env` chứa secret và phải được cấp qua secret store
trong môi trường chia sẻ thực tế.

## Backup và restore thử

Lệnh chạy ở thư mục gốc, sau khi stack đã lên. `pg_dump`/`psql` chạy bên trong
container nên không cần cài client PostgreSQL trên máy host.

```powershell
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
docker compose --env-file .env -f compose.staging.yml exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /tmp/staging.dump'
docker compose --env-file .env -f compose.staging.yml cp postgres:/tmp/staging.dump "backup-$stamp.dump"
docker compose --env-file .env -f compose.staging.yml exec -T postgres rm -f /tmp/staging.dump
```

Để kiểm tra restore mà không ảnh hưởng staging, khởi động một PostgreSQL thử với
database/user/password riêng (không mount volume staging), sau đó nạp bản dump:

```powershell
docker run --name datingapp-restore-check -e POSTGRES_DB=restore_check -e POSTGRES_USER=restore_user -e POSTGRES_PASSWORD=restore_local_only -d postgres:17
docker cp .\backup-YYYYMMDD-HHMMSS.dump datingapp-restore-check:/tmp/backup.dump
docker exec datingapp-restore-check pg_restore -U restore_user -d restore_check --clean --if-exists /tmp/backup.dump
docker exec datingapp-restore-check psql -U restore_user -d restore_check -c '\dt'
docker rm -f datingapp-restore-check
```

Thay tên file dump bằng file vừa tạo. Giữ bản backup ở nơi lưu trữ riêng có kiểm soát quyền,
đặt chính sách lưu giữ/xóa cùng nhóm vận hành. Không backup vào Git.

## Giới hạn hiện tại

Repository đã có Identity cookie Auth, migration Identity/SafetyAdmin và account
status service. Cấu hình Compose chưa tự apply migration hoặc cấp role Admin; cần
thực hiện các bước đó trước khi dùng chức năng nghiệp vụ trên staging.

Auth ở Production dùng cookie Secure, nên địa chỉ HTTP local phía trên chỉ phù hợp
kiểm tra hạ tầng. Cần HTTPS và xác minh cấu hình reverse proxy/cookie trước khi thử
đăng nhập trên staging. SignalR Hub và tích hợp block với match/chat vẫn cần TV3/TV4
hoàn thiện; report Message/Post hiện trả 501. Đây chưa phải bản phát hành production.
