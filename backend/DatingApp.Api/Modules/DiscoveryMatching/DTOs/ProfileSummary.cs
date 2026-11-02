namespace DatingApp.Api.Modules.DiscoveryMatching.DTOs;

/// <summary>Shared projection agreed with Discovery; contains no email or date of birth.</summary>
public sealed record ProfileSummary(
    Guid UserId,
    string DisplayName,
    int Age,
    string City,
    string? Occupation,
    string? Bio,
    IReadOnlyList<string> Interests,
    string? PhotoUrl,
    int? DistanceKm);
