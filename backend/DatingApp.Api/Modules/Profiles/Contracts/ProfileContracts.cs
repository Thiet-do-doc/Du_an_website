namespace DatingApp.Api.Modules.Profiles.Contracts;

public sealed record UpdateProfileRequest(string DisplayName, string? Bio, string? City, string? Gender,
    bool IsDiscoverable, IReadOnlyCollection<Guid> InterestIds);

// The owner-only contract includes profile controls and completion state.
public sealed record MyProfileResponse(Guid UserId, string DisplayName, int Age, string? Bio, string? City,
    string? Gender, bool IsDiscoverable, bool IsComplete, IReadOnlyCollection<PhotoResponse> Photos,
    IReadOnlyCollection<InterestResponse> Interests);

// Public profile data is deliberately a separate contract: it has no email, exact DOB,
// discoverability switch, or private completion state.
public sealed record PublicProfileResponse(Guid UserId, string DisplayName, int Age, string? Bio,
    string? City, string? Gender, IReadOnlyCollection<PhotoResponse> Photos,
    IReadOnlyCollection<InterestResponse> Interests);

public sealed record PhotoResponse(Guid Id, string Url, bool IsAvatar, int SortOrder);
public sealed record InterestResponse(Guid Id, string Name);
public sealed record ReorderPhotosRequest(IReadOnlyCollection<Guid> PhotoIds);
