using System.Security.Claims;
using DatingApp.Api.Modules.DiscoveryMatching.DTOs;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DatingApp.Api.Modules.DiscoveryMatching.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1")]
public sealed class DiscoveryController : ControllerBase
{
    [HttpGet("discover")]
    public async Task<IActionResult> Discover([FromQuery] DiscoveryQuery query, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try { return Ok(await HttpContext.RequestServices.GetRequiredService<DiscoveryService>().SearchAsync(userId, query, cancellationToken)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Bộ lọc không hợp lệ", detail: ex.Message); }
        catch (ProfileNotCompleteException ex) { return Problem(statusCode: 409, title: "Hồ sơ chưa hoàn thiện", detail: ex.Message); }
        catch (DiscoveryDependencyUnavailableException ex) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: ex.Message); }
        catch (InvalidOperationException) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: "Database hoặc module phụ thuộc chưa được cấu hình."); }
    }

    [HttpPost("user-likes/{targetUserId:guid}")]
    public async Task<IActionResult> Like(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            if (!await CanInteractAsync(userId, targetUserId, cancellationToken)) return NotFound();
            return Ok(await HttpContext.RequestServices.GetRequiredService<IMatchService>().LikeAsync(userId, targetUserId, cancellationToken));
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Lượt thích không hợp lệ", detail: ex.Message); }
        catch (ProfileNotCompleteException ex) { return Problem(statusCode: 409, title: "Hồ sơ chưa hoàn thiện", detail: ex.Message); }
        catch (MatchUnavailableException ex) { return Problem(statusCode: 409, title: "Không thể ghép đôi", detail: ex.Message); }
        catch (DiscoveryDependencyUnavailableException ex) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: ex.Message); }
        catch (InvalidOperationException) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: "Database hoặc module phụ thuộc chưa được cấu hình."); }
    }

    [HttpPost("passes/{targetUserId:guid}")]
    public async Task<IActionResult> Pass(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            if (!await CanInteractAsync(userId, targetUserId, cancellationToken)) return NotFound();
            await HttpContext.RequestServices.GetRequiredService<IMatchService>().PassAsync(userId, targetUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Lượt bỏ qua không hợp lệ", detail: ex.Message); }
        catch (ProfileNotCompleteException ex) { return Problem(statusCode: 409, title: "Hồ sơ chưa hoàn thiện", detail: ex.Message); }
        catch (DiscoveryDependencyUnavailableException ex) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: ex.Message); }
        catch (InvalidOperationException) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: "Database hoặc module phụ thuộc chưa được cấu hình."); }
    }

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var matching = HttpContext.RequestServices.GetRequiredService<IMatchService>();
            var blocked = await HttpContext.RequestServices.GetRequiredService<IBlockLookup>().GetBlockedUserIdsAsync(userId, cancellationToken);
            var active = (await matching.GetMatchesAsync(userId, cancellationToken)).Where(x => !blocked.Contains(x.OtherUserId)).ToArray();
            var profiles = await HttpContext.RequestServices.GetRequiredService<IDiscoveryProfileQuery>()
                .GetByIdsAsync(userId, active.Select(x => x.OtherUserId).ToArray(), cancellationToken);
            var byId = profiles.ToDictionary(x => x.UserId);
            return Ok(active.Where(x => byId.ContainsKey(x.OtherUserId))
                .Select(x => new MatchDetailsResponse(x.Id, x.OtherUserId, x.ConversationId, x.MatchedAt, x.IsActive, byId[x.OtherUserId])));
        }
        catch (DiscoveryDependencyUnavailableException ex) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: ex.Message); }
        catch (InvalidOperationException) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: "Database hoặc module phụ thuộc chưa được cấu hình."); }
    }

    [HttpDelete("matches/{matchId:guid}")]
    public async Task<IActionResult> Unmatch(Guid matchId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try { return await HttpContext.RequestServices.GetRequiredService<IMatchService>().UnmatchAsync(userId, matchId, cancellationToken) ? NoContent() : NotFound(); }
        catch (DiscoveryDependencyUnavailableException ex) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: ex.Message); }
        catch (InvalidOperationException) { return Problem(statusCode: 503, title: "Chưa sẵn sàng", detail: "Database hoặc module phụ thuộc chưa được cấu hình."); }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        if (User.Identity?.IsAuthenticated != true) return false;
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out userId) && userId != Guid.Empty;
    }

    private async Task<bool> CanInteractAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        if (targetUserId == userId) return true;
        var profileQuery = HttpContext.RequestServices.GetRequiredService<IDiscoveryProfileQuery>();
        if (!await profileQuery.IsCompleteAsync(userId, cancellationToken))
            throw new ProfileNotCompleteException();
        var target = (await profileQuery.GetByIdsAsync(userId, [targetUserId], cancellationToken))
            .SingleOrDefault(x => x.UserId == targetUserId);
        if (target is null) return false;
        var blocked = await HttpContext.RequestServices.GetRequiredService<IBlockLookup>()
            .GetBlockedUserIdsAsync(userId, cancellationToken);
        return !blocked.Contains(targetUserId);
    }
}
