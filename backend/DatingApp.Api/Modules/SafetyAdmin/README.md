# SafetyAdmin
- Chủ trì: **TV5**. Dự phòng: **TV1**.
- Phạm vi: Block, report, quản trị, khóa tài khoản và audit.
- Bảng dự kiến: Blocks, Reports, ModerationActions.
- Frontend tương ứng: `frontend/src/features/safety-admin/`.

Đây là chỗ phát triển tiếp, chưa có endpoint nghiệp vụ.

## Đầu việc đầu tiên
1. Chốt request/response DTO và quy tắc quyền với thành viên liên quan.
2. Tạo entity, cấu hình EF Core và migration có review khi đã chốt schema.
3. Xây service xử lý nghiệp vụ; controller chỉ nhận, kiểm tra và trả HTTP.
4. Nối giao diện React với API thật, có loading/error/empty.
5. Thêm kiểm thử sai quyền và quy tắc quan trọng; gửi PR cho người khác review.

Không tạo bảng hoặc API trùng với module khác. Không phát triển trước Auth
bằng cách cho phép mọi người thao tác dữ liệu. Trao đổi API qua `Shared/Contracts`
và service nếu cần thay đổi dữ liệu thuộc module khác.
# Block policy

- A block is directional: the signed-in user owns the block and can remove it.
- Users cannot block themselves. Repeating a block or unblock is safe and returns success.
- Store only user IDs and UTC creation time; do not expose the blocker's identity to the blocked user.
- Before creating matches/conversations or sending messages, the owning modules must reject the operation if either user has blocked the other. A block should also prevent discovery in both directions. This check must be done server-side.
- Do not add a foreign key to the user table until the Auth module's user entity and key schema are merged.

## API

All endpoints require a valid bearer token whose `sub` (or `NameIdentifier`) is a UUID.

| Method | Path | Behavior |
|---|---|---|
| GET | `/api/v1/blocks` | List the signed-in user's blocks, newest first |
| PUT | `/api/v1/blocks/{userId}` | Block; idempotent, 400 for self-block |
| DELETE | `/api/v1/blocks/{userId}` | Unblock; idempotent |

The React functions are in `frontend/src/features/safety-admin/api/blocks.ts`.
They accept the access token from the future Auth integration; no token is stored by this module.

## Integration still needed

The shared app currently has no authentication scheme, Identity user entity, or business migration. Auth must register bearer authentication/authorization before these endpoints can run. The team migration owner must generate and review a migration for `user_blocks`. Discovery/matching and chat must call the bidirectional block policy before their operations are enabled. Until these are connected, this is the module's persistence/API foundation, not a complete user-facing safety flow.
