# SafetyAdmin — Khuyến (#21)

Dùng Identity Guid, JWT và ICurrentUser từ nhánh chung. API chặn: GET danh sách,
POST /api/v1/blocks/{targetUserId}, DELETE cùng đường dẫn. Lặp lại an toàn;
IBlockPolicy kiểm tra hai chiều. UI thật tại /blocks, dùng authenticatedFetch.

Xem [bàn giao #21](../../../../docs/ISSUE_21_HANDOFF.md) cho DTO, test, migration,
quy tắc ẩn hội thoại và ví dụ discovery/chat. Xem [vận hành](../../../../infra/OPERATIONS.md).
Chưa xác nhận chặn xuyên hệ thống cho tới khi người phụ trách ghép policy vào module.

Report/Admin hiện có được giữ lại, ngoài phạm vi nghiệm thu tuần 2. Report Message/Post
vẫn trả 501 cho tới khi có kiểm tra quyền từ module sở hữu. API Admin yêu cầu role Admin.
