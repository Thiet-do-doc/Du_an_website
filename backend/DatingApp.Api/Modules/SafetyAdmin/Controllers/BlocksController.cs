using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;
[ApiController]
[Authorize]
[Route("api/v1/blocks")]
public sealed class BlocksController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid actorId) return Unauthorized();
        return Ok(await db.Blocks.AsNoTracking().Where(x => x.BlockerUserId == actorId)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { userId = x.BlockedUserId, blockedAt = x.CreatedAt })
            .ToListAsync(cancellationToken));
    }
    [HttpPost("{targetUserId}")]
    public async Task<IActionResult> Block(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid actorId) return Unauthorized();
        if (targetUserId == Guid.Empty || actorId == targetUserId)
            return BadRequest(new { message = "ID không hợp lệ hoặc bạn đang tự chặn mình." });
        if (!await db.Users.AnyAsync(x => x.Id == targetUserId, cancellationToken)) return NotFound();
        // Atomic repeat handling; other database errors remain visible.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_blocks (blocker_user_id, blocked_user_id, created_at)
            VALUES ({actorId}, {targetUserId}, {DateTimeOffset.UtcNow})
            ON CONFLICT (blocker_user_id, blocked_user_id) DO NOTHING
            """, cancellationToken);
        return NoContent();
    }
    [HttpDelete("{targetUserId}")]
    public async Task<IActionResult> Unblock(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid actorId) return Unauthorized();
        if (targetUserId == Guid.Empty || actorId == targetUserId) return BadRequest();
        await db.Blocks.Where(x => x.BlockerUserId == actorId && x.BlockedUserId == targetUserId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }
}
