using DatingApp.Api.Modules.ChatNotifications.Services;
using Xunit;

namespace DatingApp.Tests;

public sealed class TV4ChatRulesTests
{
    [Fact]
    public void MessageRules_TrimsContent() =>
        Assert.Equal("Xin chào", MessageRules.Validate("  Xin chào  ", Guid.NewGuid()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MessageRules_RejectsEmptyContent(string value) =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate(value, Guid.NewGuid()));

    [Fact]
    public void MessageRules_RejectsMoreThanTwoThousandCharacters() =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate(new string('a', 2001), Guid.NewGuid()));

    [Fact]
    public void MessageRules_RequiresClientMessageId() =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate("Hello", Guid.Empty));

    [Fact]
    public void Cursor_RoundTripsUtcTimestampAndGuid()
    {
        var time = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var encoded = CursorCodec.Encode(time, id);
        Assert.True(CursorCodec.TryDecode(encoded, out var decodedTime, out var decodedId));
        Assert.Equal(time.UtcTicks, decodedTime.UtcTicks);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    public void Cursor_RejectsInvalidInput(string cursor) =>
        Assert.False(CursorCodec.TryDecode(cursor, out _, out _));

    [Fact]
    public void AccessRules_ActiveMatchAllowsMemberToReadAndSend()
    {
        var (low, high, match, conversation) = Fixture();
        var result = ChatAccessRules.Evaluate(conversation, low, low, high, match, true, true);
        Assert.True(result.CanRead);
        Assert.True(result.CanSend);
        Assert.Equal(high, result.PartnerId);
    }

    [Fact]
    public void AccessRules_UnmatchedConversationIsReadOnly()
    {
        var (low, high, match, conversation) = Fixture();
        match = match with { UnmatchedAtUtc = DateTime.UtcNow };
        var result = ChatAccessRules.Evaluate(conversation, low, low, high, match, true, true);
        Assert.True(result.CanRead);
        Assert.False(result.CanSend);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void AccessRules_BlockOrInactiveAccountDeniesReadAndSend(bool active, bool canInteract)
    {
        var (low, high, match, conversation) = Fixture();
        var result = ChatAccessRules.Evaluate(conversation, low, low, high, match, active, canInteract);
        Assert.False(result.CanRead);
        Assert.False(result.CanSend);
    }

    [Fact]
    public void AccessRules_NonMemberCannotReadConversation()
    {
        var (low, high, match, conversation) = Fixture();
        var outsider = Guid.NewGuid();
        var result = ChatAccessRules.Evaluate(conversation, outsider, low, high, match, true, true);
        Assert.False(result.CanRead);
        Assert.False(result.CanSend);
    }

    [Fact]
    public void AccessRules_ConversationMustMatchMatchParticipants()
    {
        var (low, high, match, conversation) = Fixture();
        var unrelated = Guid.NewGuid();
        var result = ChatAccessRules.Evaluate(conversation, low, low, unrelated, match, true, true);
        Assert.False(result.CanRead);
    }

    [Fact]
    public void ReportTarget_AllowsParticipantToReportOtherParticipantsMessage()
    {
        var reporter = Guid.NewGuid();
        var sender = Guid.NewGuid();
        var low = reporter.CompareTo(sender) < 0 ? reporter : sender;
        var high = reporter.CompareTo(sender) < 0 ? sender : reporter;
        Assert.True(ChatReportTargetRules.CanReportMessage(reporter, Guid.NewGuid(), sender, low, high));
    }

    [Fact]
    public void ReportTarget_RejectsMessageFromSelfOrNonParticipant()
    {
        var low = Guid.NewGuid();
        var high = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var message = Guid.NewGuid();
        Assert.False(ChatReportTargetRules.CanReportMessage(low, message, low, low, high));
        Assert.False(ChatReportTargetRules.CanReportMessage(outside, message, high, low, high));
    }

    [Fact]
    public void ReportTarget_RejectsInvalidIdsAndNonParticipantSender()
    {
        var low = Guid.NewGuid();
        var high = Guid.NewGuid();
        Assert.False(ChatReportTargetRules.CanReportMessage(low, Guid.Empty, high, low, high));
        Assert.False(ChatReportTargetRules.CanReportMessage(low, Guid.NewGuid(), Guid.NewGuid(), low, high));
    }

    private static (Guid Low, Guid High, ChatMatchSnapshot Match, Guid Conversation) Fixture()
    {
        var low = Guid.NewGuid();
        var high = Guid.NewGuid();
        if (low.CompareTo(high) > 0) (low, high) = (high, low);
        var matchId = Guid.NewGuid();
        return (low, high, new ChatMatchSnapshot(matchId, low, high, null), Guid.NewGuid());
    }
}
