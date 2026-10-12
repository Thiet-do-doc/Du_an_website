using System.Security.Claims;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DatingApp.Api.Modules.ChatNotifications.Hubs;

[Authorize]
public class ChatHub(ChatService chats, IChatPermissions permissions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = UserId();
        if (!await permissions.IsUserActiveAsync(userId))
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var access = await permissions.GetAccessAsync(conversationId, UserId());
        if (!access.CanRead) throw new HubException("Không có quyền tham gia hội thoại.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(conversationId));
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(conversationId));

    public async Task<MessageDto> SendMessage(Guid conversationId, string content, Guid clientMessageId)
    {
        var userId = UserId();
        SendResult result;
        try
        {
            result = await chats.SendMessageAsync(conversationId, userId, content, clientMessageId);
        }
        catch (ChatDomainException e)
        {
            throw new HubException(e.Message);
        }

        // Message goes directly to approved users, NEVER to a stale group membership.
        // A group is not an authorization boundary: a user can be blocked mid-connection.
        if (result.IsNew)
        {
            var access = await permissions.GetAccessAsync(conversationId, userId);
            if (access.CanRead && await permissions.IsUserActiveAsync(result.PartnerId))
            {
                await Clients.User(userId.ToString()).SendAsync("MessageCreated", result.Message);
                await Clients.User(result.PartnerId.ToString()).SendAsync("MessageCreated", result.Message);
                if (result.Notification is not null)
                    await Clients.User(result.PartnerId.ToString())
                        .SendAsync("NotificationCreated", result.Notification);
            }
        }
        // Invoke return value doubles as the sender's acknowledgement (idempotent retry).
        return result.Message;
    }

    private Guid UserId() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
        out var value) ? value : throw new HubException("Phiên đăng nhập không hợp lệ.");
    private static string Group(Guid id) => $"conversation:{id:N}";
}
