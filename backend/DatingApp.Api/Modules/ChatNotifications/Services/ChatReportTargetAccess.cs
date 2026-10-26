using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

/// <summary>
/// Contract for TV5's report endpoint to verify a referenced message without disclosing its content.
/// The reporter must be a participant of the conversation and the target message must belong to the
/// other participant. This check intentionally does not require the match to remain active or the
/// pair to remain unblocked: users must still be able to report a previously received message after
/// unmatch/block. The report endpoint must not return the message body to the reporter.
/// </summary>
public interface IChatReportTargetAccess
{
    Task<bool> CanReportMessageAsync(Guid reporterId, Guid messageId,
        CancellationToken cancellationToken = default);
}

public sealed class ChatReportTargetAccess(AppDbContext db) : IChatReportTargetAccess
{
    public async Task<bool> CanReportMessageAsync(Guid reporterId, Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (reporterId == Guid.Empty || messageId == Guid.Empty) return false;

        var target = await (
            from message in db.Messages.AsNoTracking()
            join conversation in db.Conversations.AsNoTracking()
                on message.ConversationId equals conversation.Id
            where message.Id == messageId
            select new
            {
                message.SenderId,
                conversation.UserLowId,
                conversation.UserHighId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null) return false;
        return ChatReportTargetRules.CanReportMessage(reporterId, messageId, target.SenderId,
            target.UserLowId, target.UserHighId);
    }
}

/// <summary>Pure target rule used by TV4's unit tests and TV5's report authorization contract.</summary>
public static class ChatReportTargetRules
{
    public static bool CanReportMessage(Guid reporterId, Guid messageId, Guid senderId,
        Guid conversationUserLowId, Guid conversationUserHighId)
    {
        if (reporterId == Guid.Empty || messageId == Guid.Empty || senderId == Guid.Empty ||
            conversationUserLowId == Guid.Empty || conversationUserHighId == Guid.Empty ||
            conversationUserLowId == conversationUserHighId)
            return false;

        var isParticipant = reporterId == conversationUserLowId || reporterId == conversationUserHighId;
        return isParticipant && senderId != reporterId &&
            (senderId == conversationUserLowId || senderId == conversationUserHighId);
    }
}
