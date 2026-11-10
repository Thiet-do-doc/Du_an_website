# Bài thực hành 2 — Jenkins và GitHub cho Mylove

## 1. Mục tiêu và kiến trúc

Sử dụng dự án có sẵn tại https://github.com/Thiet-do-doc/Du_an_website.
Frontend là React/TypeScript/Vite; backend là ASP.NET Core .NET 10; database PostgreSQL 17.
Chọn phương án **Jenkins chạy trong Docker Container Linux** trên Docker Desktop/WSL2.

```text
GitHub / codex/jenkins-cloud-lab
          | checkout + Poll SCM mỗi khoảng 5 phút
          v
Jenkins :8081 -> Restore -> Build -> Migration DB test -> Backend test
          -> Frontend test -> Package -> Báo cáo test và artifacts
                     |
             PostgreSQL test riêng
```

Jenkins chạy bằng user `jenkins`, có tài khoản đăng nhập, volume giữ lịch sử, chỉ công khai cổng trên localhost. Container chứa sẵn .NET SDK 10 và Node 24 để thống nhất môi trường. Bài lab chạy một executor trên controller cho một repository tin cậy; hệ thống nhiều nhóm/production nên dùng agent riêng. Không mount Docker socket vào Jenkins.

## 2. Chuẩn bị và khởi động

Yêu cầu Docker Desktop ở chế độ Linux containers, Git, kết nối Internet. Lần build đầu phải tải các image và plugin nên có thể lâu. Máy Windows không cần cài riêng Java/.NET/Node đúng phiên bản để chạy pipeline trong container.

Tại PowerShell:

```powershell
cd C:\Users\Admin\Du_an_website
git switch codex/jenkins-cloud-lab
docker version
./scripts/Initialize-JenkinsLab.ps1
docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml config --quiet
docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml up -d --build
docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml ps
```

Mở http://localhost:8081. Username: `student`. Xem mật khẩu riêng trên máy:

```powershell
Get-Content .artifacts/jenkins/admin-password.txt
```

Không đưa mật khẩu vào báo cáo, ảnh chụp hoặc Git. Script khởi tạo giữ nguyên mật khẩu đã tạo. File `.env` và mật khẩu nằm trong `.artifacts/` đã được Git ignore. Groovy chỉ khởi tạo tài khoản/job khi volume mới chưa có marker; sửa cấu hình job đang tồn tại bằng giao diện Jenkins.

## 3. Plugins và testing frameworks

Dockerfile dùng `jenkins-plugin-cli` cài danh sách `infra/jenkins/plugins.txt`.

| Plugin | Vai trò |
|---|---|
| Pipeline (`workflow-aggregator`) | Đọc và thực thi Declarative Jenkinsfile |
| Git và GitHub | Tích hợp repository và checkout source |
| JUnit | Hiển thị báo cáo XML từ Node test runner |
| MSTest | Nhập báo cáo TRX của `dotnet test` |
| Pipeline Stage View | Hiển thị kết quả từng stage |
| Credentials Binding | Hỗ trợ credential cho các cấu hình mở rộng |
| Timestamper | Ghi thời gian vào Console Output |

Framework backend vẫn là **xUnit**, không đổi sang MSTest. Tên plugin MSTest chỉ là bộ đọc định dạng TRX. Frontend dùng **node:test** và **node:assert** có sẵn trong Node.js. Dependencies của ứng dụng được phục hồi bằng `dotnet restore` và `npm ci`.

## 4. Tích hợp GitHub và Jenkinsfile

Job `Mylove-CI` được khởi tạo tự động với cấu hình tương đương:

1. New Item → `Mylove-CI` → Pipeline.
2. Definition: **Pipeline script from SCM**.
3. SCM: **Git**; URL: `https://github.com/Thiet-do-doc/Du_an_website.git`.
4. Branch Specifier: `*/codex/jenkins-cloud-lab`.
5. Script Path: `Jenkinsfile`; bật Lightweight checkout.
6. Save → Build Now để chạy lần đầu và đăng ký trigger.

Repository công khai nên checkout không cần token. Với repository private, thêm token chỉ có quyền đọc repository vào Jenkins Credentials, kiểu Username with password; chọn credential trong SCM. Không viết token trong Jenkinsfile.

Pipeline dùng `pollSCM('H/5 * * * *')`: Jenkins kiểm tra GitHub theo lịch, chỉ chạy khi source thay đổi. `H` phân bố thời điểm polling; không phải cứ đúng phút 00,05. Lần đầu cần Build Now để pipeline đăng ký lịch. Đây là tích hợp tự động phù hợp máy localhost; GitHub không gửi webhook trực tiếp đến localhost được.

Để minh họa: sửa một câu trong tài liệu trên đúng nhánh, commit/push, chờ chu kỳ polling; kiểm tra nguyên nhân build **Started by an SCM change** và commit SHA trong Console Output. Nếu dùng server công khai sau này, có thể cấu hình webhook tới `/github-webhook/`, rồi dùng trigger GitHub thay cho polling.

## 5. Các stage và điều kiện thành công

| Stage | Công việc | Bằng chứng |
|---|---|---|
| Checkout GitHub | Xóa workspace cũ, `checkout scm`, in SHA và phiên bản | Console Output |
| Restore | Khôi phục NuGet và npm theo project/lockfile | Lệnh trả mã 0 |
| Build | .NET Release, TypeScript và Vite production | Build succeeded; frontend dist |
| Prepare test database | Áp EF migrations; kiểm tra model chưa có migration | Migration/model check thành công |
| Backend tests | Chạy xUnit với PostgreSQL test thật | `test-results/backend/backend.trx` |
| Frontend tests | Chạy các test Node | `frontend/test-results/frontend.xml` |
| Package | Publish API, nén API/frontend | Hai file `.tar.gz` trong Artifacts |

Biến `AUTH_TEST_CONNECTION` bắt buộc phải có trước khi test; thiếu biến sẽ dừng pipeline, tránh coi các test database bị skip là hoàn tất. PostgreSQL test dùng tmpfs riêng, không dùng dữ liệu website. Restart database test sẽ mất dữ liệu test; migration chạy lại ở lần build tiếp theo.

Test thất bại được giữ là **FAILURE** bằng `catchError`, đồng thời vẫn cho phép nhóm test còn lại chạy để thu đủ kết quả. `post { always { ... } }` xuất báo cáo ngay cả khi test lỗi. Package chỉ chạy khi kết quả hiện tại là SUCCESS. Mỗi build checkout sạch nên báo cáo cũ và test minh họa không lẫn sang lần chạy sau.

## 6. Thực hành phân tích và xử lý lỗi

Chạy bình thường với `DEMO_TEST_FAILURE=false`. Sau lần đầu, vào Build with Parameters và bật `DEMO_TEST_FAILURE=true`. Pipeline sinh trong workspace một test có kỳ vọng sai `1 + 1 = 3`; đây là lỗi chủ động phục vụ bài học, không sửa mã sản phẩm trên GitHub.

Kết quả cần quan sát: Frontend tests FAILURE, có test `LAB intentional failure`, Package bị bỏ qua, log hiển thị expected 3 và actual 2. Xử lý: đọc Test Result/stack trace, xác định assertion sai, chạy lại với tham số false. Checkout sạch loại test minh họa và pipeline phải trở lại SUCCESS. Khi test sản phẩm thật bị lỗi, sửa implementation hoặc kỳ vọng theo đặc tả rồi commit/push; không tắt test để làm xanh pipeline.

| Hiện tượng | Nguyên nhân cần kiểm tra | Cách xử lý |
|---|---|---|
| Không có Docker Server hoặc lỗi pipe | Docker Desktop chưa chạy/WSL chưa sẵn sàng | Mở Docker Desktop, đợi Engine running, chạy `docker version` |
| Port 8080 đã dùng | Website đang chạy | Lab đã chọn 8081, không dừng website |
| Node local khác Node yêu cầu | Máy có Node 22, dự án cần 24 | Build/test bằng Node 24 trong container |
| Không lấy được Jenkinsfile | Chưa push file hoặc sai branch | Kiểm tra GitHub và Branch Specifier |
| NuGet/npm/plugin timeout | Kết nối mạng hoặc nguồn package | Đọc đúng URL lỗi, khôi phục mạng, chạy lại |
| Không có báo cáo test | Test runner lỗi trước khi ghi file/sai đường dẫn | Xem log stage; không bật allowEmptyResults để che lỗi |
| Connection refused PostgreSQL | DB chưa healthy/sai host | Dùng host `postgres-test` trong container, kiểm tra compose ps |
| Có pending model changes | Model và migration không đồng bộ | Tạo/review migration hợp lệ rồi commit, không bỏ kiểm tra |

## 7. Thu thập bằng chứng và báo cáo

Cần trình bày: mục tiêu, dự án/URL GitHub, môi trường, kiến trúc, các lệnh đã chạy, plugin, Jenkinsfile, kết quả từng stage, số test pass/fail/skip, lỗi và cách khắc phục, kết luận. Phân biệt lần lỗi chủ động với lỗi phát sinh thật.

Ảnh nên chụp: `docker compose ps`; Manage Jenkins → Plugins → Installed; cấu hình Pipeline from SCM; lịch sử build; stage view của build thành công; test report; build lỗi minh họa và lần chạy lại thành công. Che thông tin đăng nhập. Kèm link source và file log đã kiểm tra không chứa bí mật.

Thông tin nhóm, MSSV, lớp, giảng viên và link nộp bài cần điền theo lớp học. Không khẳng định đã nộp khi chưa tải lên đúng biểu mẫu bài thực hành số 2.

## 8. Dừng và chạy lại

```powershell
docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml stop
docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml up -d
```

Volume Jenkins giữ cấu hình/lịch sử. Không dùng `down -v` trừ khi chủ động muốn xóa toàn bộ dữ liệu Jenkins lab. Sau khi nộp, có thể tắt Poll SCM trong job/Jenkinsfile để tránh build không cần thiết.

## Tài liệu chính thức

- Jenkins Docker: https://www.jenkins.io/doc/book/installing/docker/
- Pipeline syntax và Poll SCM: https://www.jenkins.io/doc/book/pipeline/syntax/
- MSTest TRX publisher: https://www.jenkins.io/doc/pipeline/steps/mstest/
- JUnit publisher: https://www.jenkins.io/doc/pipeline/steps/junit/
- Node test runner: https://nodejs.org/docs/latest-v24.x/api/test.html
