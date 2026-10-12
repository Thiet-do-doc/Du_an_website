using System.Security.Claims;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.Api.Modules.ChatNotifications.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations")]
public class ConversationsController(ChatService service, IChatPermissions permissions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId, ct)) return Forbid();
        return Ok(await service.ListConversationsAsync(userId, page, pageSize, ct));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] int limit = 30,
        [FromQuery] string? cursor = null, [FromQuery] Guid? afterId = null,
        CancellationToken ct = default)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (!await permissions.IsUserActiveAsync(userId, ct)) return Forbid();
        try
        {
            return Ok(await service.GetMessagesAsync(id, userId, limit, cursor, afterId, ct));
        }
        catch (ChatDomainException e)
        {
            return Problem(statusCode: e.StatusCode, title: e.Message);
        }
    }

    private bool TryUser(out Guid id) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}
