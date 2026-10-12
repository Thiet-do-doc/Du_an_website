using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/blocks")]
public sealed class BlocksController(AppDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPost("{targetUserId}")]
    public async Task<IActionResult> Block(string targetUserId, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Unauthorized();
        if (actorId == targetUserId) return BadRequest(new { message = "Bạn không thể tự chặn mình." });
        if (await users.FindByIdAsync(targetUserId) is null) return NotFound();
        var exists = await db.Blocks.AnyAsync(x => x.BlockerUserId == actorId && x.BlockedUserId == targetUserId, cancellationToken);
        if (exists) return NoContent();
        db.Blocks.Add(new UserBlock { Id = Guid.NewGuid(), BlockerUserId = actorId, BlockedUserId = targetUserId });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            // A concurrent repeat is still an idempotent block.
            if (!await db.Blocks.AnyAsync(x => x.BlockerUserId == actorId && x.BlockedUserId == targetUserId, cancellationToken)) throw;
        }
        return NoContent();
    }

    [HttpDelete("{targetUserId}")]
    public async Task<IActionResult> Unblock(string targetUserId, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Unauthorized();
        var block = await db.Blocks.SingleOrDefaultAsync(x => x.BlockerUserId == actorId && x.BlockedUserId == targetUserId, cancellationToken);
        if (block is not null) { db.Blocks.Remove(block); await db.SaveChangesAsync(cancellationToken); }
        return NoContent();
    }
}
