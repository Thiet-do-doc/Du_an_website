namespace DatingApp.Api.Modules.DiscoveryMatching.DTOs;


public sealed record DiscoveryQuery(
    int Page = 1,
    int PageSize = 20,
    int? MinAge = 18,
    int? MaxAge = 35,
    string? City = null,
    string? Interest = null);

public sealed record DiscoveryPage(IReadOnlyList<ProfileSummary> Items, int Page, int PageSize, bool HasMore);
public sealed record LikeResponse(bool IsMatch, Guid? MatchId, Guid? ConversationId, DateTime? MatchedAt);
public sealed record MatchResponse(Guid Id, Guid OtherUserId, Guid ConversationId, DateTime MatchedAt, bool IsActive);
public sealed record MatchDetailsResponse(Guid Id, Guid OtherUserId, Guid ConversationId, DateTime MatchedAt, bool IsActive, ProfileSummary OtherProfile);
