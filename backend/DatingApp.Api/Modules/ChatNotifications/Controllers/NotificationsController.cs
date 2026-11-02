using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.Api.Modules.ChatNotifications.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(NotificationService service, IChatPermissions permissions,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] string? cursor = null,
        [FromQuery] Guid? afterId = null, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId.Value, ct)) return Forbid();
        try { return Ok(await service.ListAsync(userId.Value, page, pageSize, cursor, afterId, ct)); }
        catch (ChatDomainException e) { return Problem(statusCode: e.StatusCode, title: e.Message); }
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId.Value, ct)) return Forbid();
        try { return Ok(await service.MarkReadAsync(userId.Value, id, ct)); }
        catch (ChatDomainException e) { return Problem(statusCode: e.StatusCode, title: e.Message); }
    }
}
