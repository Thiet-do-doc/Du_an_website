using DatingApp.Api.Modules.ChatNotifications.Services;
using Xunit;

namespace DatingApp.Tests;

public class ChatRulesTests
{
    [Fact]
    public void Validate_TrimsText() =>
        Assert.Equal("Xin chào", MessageRules.Validate("  Xin chào  ", Guid.NewGuid()));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_RejectsEmpty(string text) =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate(text, Guid.NewGuid()));

    [Fact]
    public void Validate_RejectsOver2000Characters() =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate(new string('a', 2001), Guid.NewGuid()));

    [Fact]
    public void Validate_RejectsEmptyClientMessageId() =>
        Assert.Throws<ChatDomainException>(() => MessageRules.Validate("A", Guid.Empty));

    [Fact]
    public void Cursor_RoundTripsTimeAndUuid()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var encoded = CursorCodec.Encode(timestamp, id);
        Assert.True(CursorCodec.TryDecode(encoded, out var decodedAt, out var decodedId));
        Assert.Equal(timestamp.UtcTicks, decodedAt.UtcTicks);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("")]
    public void Cursor_RejectsInvalidInput(string data) =>
        Assert.False(CursorCodec.TryDecode(data, out _, out _));
}
