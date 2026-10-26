using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.Api.Modules.ChatNotifications.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/conversations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConversationsController(ChatService service, IChatPermissions permissions,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId.Value, ct)) return Forbid();
        try { return Ok(await service.ListConversationsAsync(userId.Value, page, pageSize, ct)); }
        catch (ChatDependencyUnavailableException e) { return Problem(statusCode: 503, title: e.Message); }
        catch (ChatDomainException e) { return Problem(statusCode: e.StatusCode, title: e.Message); }
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] int limit = 30,
        [FromQuery] string? cursor = null, [FromQuery] Guid? afterId = null,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId.Value, ct)) return Forbid();
        try { return Ok(await service.GetMessagesAsync(id, userId.Value, limit, cursor, afterId, ct)); }
        catch (ChatDependencyUnavailableException e) { return Problem(statusCode: 503, title: e.Message); }
        catch (ChatDomainException e) { return Problem(statusCode: e.StatusCode, title: e.Message); }
    }
}
