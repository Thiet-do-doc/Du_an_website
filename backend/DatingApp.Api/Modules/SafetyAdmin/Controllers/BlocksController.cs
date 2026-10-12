using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/blocks")]
public sealed class BlocksController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlockResponse>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var blocks = await db.UserBlocks.AsNoTracking()
            .Where(block => block.BlockerUserId == userId)
            .OrderByDescending(block => block.CreatedAt)
            .Select(block => new BlockResponse(block.BlockedUserId, block.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(blocks);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var blockerUserId)) return Unauthorized();
        if (userId == blockerUserId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Không thể block chính mình.");

        var exists = await db.UserBlocks.AnyAsync(
            block => block.BlockerUserId == blockerUserId && block.BlockedUserId == userId,
            cancellationToken);
        if (exists) return NoContent();

        db.UserBlocks.Add(new UserBlock
        {
            BlockerUserId = blockerUserId,
            BlockedUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var blockerUserId)) return Unauthorized();
        var block = await db.UserBlocks.FindAsync([blockerUserId, userId], cancellationToken);
        if (block is null) return NoContent();
        db.UserBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}

public sealed record BlockResponse(Guid UserId, DateTimeOffset BlockedAt);
