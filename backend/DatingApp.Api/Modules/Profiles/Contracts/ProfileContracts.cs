namespace DatingApp.Api.Modules.Profiles.Contracts;

public sealed record UpdateProfileRequest(string DisplayName, string? Bio, string? City, string? Gender,
    bool IsDiscoverable, IReadOnlyCollection<Guid> InterestIds);

public sealed record ProfileResponse(Guid UserId, string DisplayName, int Age, string? Bio, string? City,
    string? Gender, bool IsDiscoverable, bool IsComplete, IReadOnlyCollection<PhotoResponse> Photos,
    IReadOnlyCollection<InterestResponse> Interests);

public sealed record PhotoResponse(Guid Id, string Url, bool IsAvatar, int SortOrder);
public sealed record InterestResponse(Guid Id, string Name);
public sealed record ReorderPhotosRequest(IReadOnlyCollection<Guid> PhotoIds);
public sealed record ProfileSummary(Guid UserId, string DisplayName, int Age, string? Bio, string? City,
    IReadOnlyCollection<string> Interests, string? AvatarUrl);
