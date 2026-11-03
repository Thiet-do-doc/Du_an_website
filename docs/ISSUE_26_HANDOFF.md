# Khuyến — Tuần 3: báo cáo người dùng và tin nhắn (#26)

## Phạm vi và nền tích hợp

Chỉ thực hiện T3-NV5 / issue #26. Nền main đối chiếu: `623b0a219087f88a3446cf40e182cc35b69aa97b`.
Tận dụng Reports/DTO/migration có sẵn, Auth JWT của Thiết, dữ liệu Profile/Discovery của Võ, Chat và IChatReportTargetAccess của Mạnh, block tuần 2 của Khuyến. Không tự merge các PR chưa được nhóm duyệt.

Bổ sung kiểm tra chuỗi sau trim, chuẩn hóa UUID và ngăn tự report khi ID viết hoa; tách form/API dùng chung; gắn form thật vào báo cáo tin nhắn và trang /safety (được mở từ Discovery); bổ sung kiểm thử riêng #26.
Không thay đổi model/migration, admin audit, khóa tài khoản hoặc hạ tầng triển khai. Báo cáo Posts ngoài phạm vi.

## Hợp đồng API

`POST /api/v1/reports`, JWT bắt buộc:

```json
{
  "targetType": "User",
  "targetId": "UUID của người dùng hoặc tin nhắn",
  "reason": "Quấy rối",
  "description": "Mô tả ít nhất 10 ký tự sau trim"
}
```

- `targetType`: User hoặc Message; server xác định reporter từ JWT, không nhận reporter/status do client chọn.
- `targetId`: tối đa 200 ký tự theo DTO, UUID lưu dạng chuẩn; User phải tồn tại, không tự report; Message phải là tin đã nhận trong hội thoại của reporter.
- `reason`: bắt buộc, tối đa 100 ký tự, không chỉ khoảng trắng; `description`: bắt buộc, trim 10–2000 ký tự.
- 201: trả report đã lưu gồm id, reporterUserId, targetType, targetId, targetUserId, reason, description, status=Pending, createdAtUtc; các trường reviewed* chưa có giá trị.
- 400: dữ liệu sai, tự report hoặc loại đối tượng không hợp lệ; 401: chưa đăng nhập; 404: target thiếu/không có quyền; 501: Post ngoài phạm vi. Message 404 không tiết lộ nội dung chat.
- User thường không xem/sửa report của người khác qua API admin hiện có (403).
- Sau block hoặc unmatch vẫn có thể report tin đã nhận: dùng kiểm tra thành viên lịch sử, không dùng quyền gửi tin hiện tại.

## Component bàn giao cho Võ/Mạnh

`frontend/src/features/safety-admin/components/ReportForm.tsx`:

```tsx
<ReportForm key={profile.id} targetType="User" targetId={profile.id} />
<ReportForm key={message.id} targetType="Message" targetId={message.id}
  onSubmitted={() => setReportTarget(null)} />
```

Mặc định đối tượng cố định theo props. Dùng key theo ID khi chuyển đối tượng. `allowTargetSelection` chỉ dành cho trang /safety nhập đối tượng thủ công.
Form có trạng thái đang gửi, thành công, lỗi; khóa nút khi đang gửi; lỗi 404 dùng thông báo chung; dùng authenticatedFetch của Auth để refresh phiên.

Điểm tích hợp:
- DiscoveryPage hiện dẫn `/safety?user=ID`; form điền sẵn User ID.
- ChatPage: nút Báo cáo ở tin nhận mở ReportForm với đúng message ID; không dùng prompt; không hiển thị lỗi 404 thành khẳng định module chưa tích hợp.
- API `api/reports.ts` chỉ gửi bốn trường DTO và trim input; không gửi reporter/status hoặc nội dung tin nhắn.

## Kết quả kiểm chứng local — 02/11/2026

- `npm ci`, `npm run build`: đạt. `npm test`: 10/10 đạt.
- `dotnet build DatingApp.slnx --configuration Release`: đạt, 0 cảnh báo/lỗi.
- Áp toàn bộ migration lên database test riêng: đạt.
- `dotnet test DatingApp.slnx --configuration Release --no-build --verbosity quiet`, có AUTH_TEST_CONNECTION: 57/57 đạt, không skip.
- PostgreSQL local là 18 (bản cài sẵn). Workflow GitHub hiện dùng PostgreSQL 17; xem kết quả CI của PR trước khi merge.
- `scripts/Test-RepositoryLayout.ps1`, `git diff --check`: đạt.

`ReportEndpointTests.cs` kiểm tra lưu reporter từ JWT dù body/query giả mạo, Pending/thời điểm/trim/UUID; user không xem/sửa report khác; target thiếu, tự report, sai loại, dữ liệu quá ngắn/dài/trắng; người ngoài hội thoại và người gửi tin bị từ chối; không trả nội dung chat; report tin nhận sau block và unmatch vẫn thành công. Ca sai không tạo report.
`frontend/tests/reports-api.test.mjs` kiểm tra transport JWT, DTO whitelist và lỗi API không bị coi là thành công.
`IntegratedModuleTests` hiện có kiểm tra block ảnh/hồ sơ/discovery/chat và report sau block với các module thật.

## Checklist bàn giao/demo tuần 3

1. Thiết review API/Auth; Mạnh review quyền message và form trong ChatPage.
2. A/B tạo hồ sơ, match, gửi tin; B báo cáo hồ sơ A từ khám phá và tin A đã gửi trong chat. Xác nhận form pending/thành công/thất bại và row Pending trong PostgreSQL.
3. C ngoài hội thoại đổi messageId hoặc giả reporterUserId: bị từ chối, không thấy nội dung chat; báo cáo self/missing ID bị từ chối; User gọi API đọc/sửa report phải 403.
4. B block/unmatch A: tương tác chat/discovery bị chặn theo policy nhưng vẫn report được tin đã nhận.
5. Gắn kết quả CI và bằng chứng demo với commit/PR; có review rồi merge và demo lại nhánh chung. Chưa tự đánh dấu review/merge/demo đã hoàn thành.

Checklist chuẩn bị staging theo yêu cầu #26: chuẩn bị A/B/C, dữ liệu giả, bản migration chung, và lặp lại các bước 2–4 khi nhóm có môi trường. Không cần thuê máy chủ để làm hoặc push tuần 3; triển khai staging thuộc issue #31, không nằm trong PR này.
