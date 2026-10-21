using System.Security.Claims;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DatingApp.Api.Modules.ChatNotifications.Hubs;

[Authorize(Policy = "User")]
public sealed class ChatHub(ChatService chats, IChatPermissions permissions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = UserId();
        if (!await permissions.IsUserActiveAsync(userId, Context.ConnectionAborted))
        {
            Context.Abort();
            throw new HubException("Tài khoản không hoạt động hoặc chưa xác nhận email.");
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        ConversationAccess access;
        try { access = await permissions.GetAccessAsync(conversationId, UserId(), Context.ConnectionAborted); }
        catch (ChatDependencyUnavailableException e) { throw new HubException(e.Message); }
        if (!access.CanRead) throw new HubException("Không có quyền tham gia hội thoại.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(conversationId), Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(conversationId), Context.ConnectionAborted);

    public async Task<MessageDto> SendMessage(Guid conversationId, string content, Guid clientMessageId)
    {
        var senderId = UserId();
        SendResult result;
        try
        {
            result = await chats.SendMessageAsync(conversationId, senderId, content, clientMessageId,
                Context.ConnectionAborted);
        }
        catch (ChatDomainException e) { throw new HubException(e.Message); }
        catch (ChatDependencyUnavailableException e) { throw new HubException(e.Message); }

        // Re-check permission after the write. Conversation groups are not an authorization boundary:
        // a block/lock/unmatch may happen while this Hub connection remains open.
        var accessAfterWrite = await permissions.GetAccessAsync(conversationId, senderId, Context.ConnectionAborted);
        if (accessAfterWrite.CanRead && await permissions.IsUserActiveAsync(result.PartnerId, Context.ConnectionAborted))
        {
            // Re-publishing an idempotent retry is safe: clients merge by server Message.Id and notification.Id.
            await Clients.User(senderId.ToString()).SendAsync("MessageCreated", result.Message, Context.ConnectionAborted);
            await Clients.User(result.PartnerId.ToString()).SendAsync("MessageCreated", result.Message, Context.ConnectionAborted);
            if (result.Notification is not null)
                await Clients.User(result.PartnerId.ToString()).SendAsync("NotificationCreated", result.Notification,
                    Context.ConnectionAborted);
        }
        return result.Message; // invocation ACK only represents a database-committed message
    }

    private Guid UserId() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new HubException("Phiên đăng nhập không hợp lệ.");

    private static string Group(Guid id) => $"conversation:{id:N}";
}
