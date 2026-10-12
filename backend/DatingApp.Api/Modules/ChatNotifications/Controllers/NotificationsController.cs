using System.Security.Claims;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.Api.Modules.ChatNotifications.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController(NotificationService service, IChatPermissions permissions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId, ct)) return Forbid();
        return Ok(await service.ListAsync(userId, page, pageSize, ct));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId, ct)) return Forbid();
        try { return Ok(await service.MarkReadAsync(userId, id, ct)); }
        catch (ChatDomainException e) { return Problem(statusCode: e.StatusCode, title: e.Message); }
    }

    private bool TryUser(out Guid id) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}
