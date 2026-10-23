using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Profiles.Contracts;
using DatingApp.Api.Modules.Profiles.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.Profiles.Services;

public sealed class ProfileService(AppDbContext db, TimeProvider timeProvider) : IDiscoveryProfileQuery
{
    public async Task<ProfileResponse?> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null) return null;
        var profile = await db.Profiles.Include(x => x.UserInterests).ThenInclude(x => x.Interest)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        return await ToResponseAsync(user, profile, ct);
    }

    public async Task<ProfileResponse?> GetPublicAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, ct);
        if (user is null) return null;
        var profile = await db.Profiles.Include(x => x.UserInterests).ThenInclude(x => x.Interest)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IsDiscoverable, ct);
        return profile is null ? null : await ToResponseAsync(user, profile, ct);
    }

    public async Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.InterestIds is null) throw new ProfileValidationException("Danh sách sở thích không hợp lệ.");
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 60)
            throw new ProfileValidationException("Tên hiển thị phải có từ 1 đến 60 ký tự.");
        if (request.Bio?.Length > 500) throw new ProfileValidationException("Giới thiệu tối đa 500 ký tự.");
        if (request.City?.Trim().Length > 80) throw new ProfileValidationException("Thành phố tối đa 80 ký tự.");
        if (request.Gender is { Length: > 30 }) throw new ProfileValidationException("Giá trị giới tính quá dài.");
        if (request.InterestIds.Count > 20) throw new ProfileValidationException("Chọn tối đa 20 sở thích.");

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, ct)
            ?? throw new ProfileUnavailableException();
        var ids = request.InterestIds.Distinct().ToArray();
        if (ids.Length != request.InterestIds.Count || await db.Interests.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length)
            throw new ProfileValidationException("Một hoặc nhiều sở thích không hợp lệ.");

        var profile = await db.Profiles.Include(x => x.UserInterests).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null)
        {
            profile = new Profile { UserId = userId, User = user };
            db.Profiles.Add(profile);
        }
        profile.DisplayName = displayName;
        profile.Bio = NullIfBlank(request.Bio);
        profile.City = NullIfBlank(request.City);
        profile.Gender = NullIfBlank(request.Gender);
        profile.IsDiscoverable = request.IsDiscoverable;
        profile.UpdatedAtUtc = timeProvider.GetUtcNow();
        db.UserInterests.RemoveRange(profile.UserInterests);
        foreach (var id in ids) profile.UserInterests.Add(new UserInterest { UserId = userId, InterestId = id });
        await db.SaveChangesAsync(ct);
        return (await GetMineAsync(userId, ct))!;
    }

    public async Task<IReadOnlyList<InterestResponse>> GetInterestsAsync(CancellationToken ct) =>
        await db.Interests.OrderBy(x => x.Name).Select(x => new InterestResponse(x.Id, x.Name)).ToListAsync(ct);

    public async Task<ProfileSummary?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, cancellationToken);
        var profile = await db.Profiles.Include(x => x.UserInterests).ThenInclude(x => x.Interest)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IsDiscoverable, cancellationToken);
        if (user is null || profile is null) return null;
        var avatarUrl = await db.Photos.Where(x => x.UserId == userId && x.IsAvatar)
            .Select(x => (string?)$"/api/v1/photos/{x.Id}/content").SingleOrDefaultAsync(cancellationToken);
        return new ProfileSummary(userId, profile.DisplayName, Age(user.DateOfBirth), profile.Bio, profile.City,
            profile.UserInterests.Select(x => x.Interest.Name).ToArray(), avatarUrl);
    }

    public async Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, cancellationToken);
        var profile = await db.Profiles.Include(x => x.UserInterests)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return user is not null && profile is not null
            && IsComplete(user, profile);
    }

    public async Task<IReadOnlyList<ProfileSummary>> SearchAsync(Guid viewerId, ProfileSearchCriteria criteria,
        IReadOnlySet<Guid> excludedUserIds, CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty) throw new ArgumentException("CurrentUser không hợp lệ.", nameof(viewerId));
        if (criteria.Page is < 1 or > 100_000 || criteria.PageSize is < 1 or > 51)
            throw new ArgumentOutOfRangeException(nameof(criteria));

        var today = BusinessDate();
        var minimumDob = criteria.MaxAge is int maxAge ? today.AddYears(-(maxAge + 1)).AddDays(1) : DateOnly.MinValue;
        var maximumDob = criteria.MinAge is int minAge ? today.AddYears(-minAge) : today;
        var cityFilter = string.IsNullOrWhiteSpace(criteria.City) ? null : criteria.City.Trim();
        var interestFilter = string.IsNullOrWhiteSpace(criteria.Interest) ? null : criteria.Interest.Trim();
        var excludedIds = excludedUserIds.ToArray();
        var query = EligibleProfiles(today)
            .Where(x => x.UserId != viewerId && !excludedIds.Contains(x.UserId))
            .Where(x => x.User.DateOfBirth >= minimumDob && x.User.DateOfBirth <= maximumDob);
        if (cityFilter is not null)
            query = query.Where(x => x.City != null && EF.Functions.ILike(x.City, cityFilter));
        if (interestFilter is not null)
            query = query.Where(x => x.UserInterests.Any(i => EF.Functions.ILike(i.Interest.Name, interestFilter)));

        var skip = checked((criteria.Page - 1) * criteria.PageSize);
        var profiles = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.UserId)
            .Skip(skip).Take(criteria.PageSize)
            .Select(x => new ProfileSummaryRow(x.UserId, x.DisplayName, x.Bio, x.City, x.User.DateOfBirth))
            .ToListAsync(cancellationToken);
        return await ToSummariesAsync(profiles, cancellationToken);
    }

    public async Task<IReadOnlyList<ProfileSummary>> GetByIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty || userIds.Count == 0) return [];
        var ids = userIds.Where(x => x != viewerId).Distinct().ToArray();
        if (ids.Length == 0) return [];
        var profiles = await EligibleProfiles(BusinessDate()).Where(x => ids.Contains(x.UserId))
            .OrderBy(x => x.UserId)
            .Select(x => new ProfileSummaryRow(x.UserId, x.DisplayName, x.Bio, x.City, x.User.DateOfBirth))
            .ToListAsync(cancellationToken);
        return await ToSummariesAsync(profiles, cancellationToken);
    }

    private async Task<ProfileResponse> ToResponseAsync(ApplicationUser user, Profile? profile, CancellationToken ct)
    {
        var photos = profile is null ? [] : await db.Photos.Where(x => x.UserId == user.Id).OrderBy(x => x.SortOrder)
            .Select(x => new PhotoResponse(x.Id, $"/api/v1/photos/{x.Id}/content", x.IsAvatar, x.SortOrder)).ToArrayAsync(ct);
        var interests = profile?.UserInterests.OrderBy(x => x.Interest.Name)
            .Select(x => new InterestResponse(x.InterestId, x.Interest.Name)).ToArray() ?? [];
        return new ProfileResponse(user.Id, profile?.DisplayName ?? "", Age(user.DateOfBirth), profile?.Bio,
            profile?.City, profile?.Gender, profile?.IsDiscoverable ?? false,
            profile is not null && IsComplete(user, profile), photos, interests);
    }

    private bool IsComplete(ApplicationUser user, Profile profile) => !string.IsNullOrWhiteSpace(profile.DisplayName)
        && profile.UserInterests.Count > 0
        && user.EmailConfirmed && user.IsActive && Age(user.DateOfBirth) >= 18;
    private int Age(DateOnly dob)
    {
        var today = BusinessDate();
        return today.Year - dob.Year - (today < dob.AddYears(today.Year - dob.Year) ? 1 : 0);
    }
    private DateOnly BusinessDate() => DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    private IQueryable<Profile> EligibleProfiles(DateOnly today) => db.Profiles.AsNoTracking()
        .Where(x => x.IsDiscoverable && x.User.IsActive && x.User.EmailConfirmed
            && x.DisplayName.Trim() != ""
            && x.User.DateOfBirth <= today.AddYears(-18)
            && x.UserInterests.Any());

    private async Task<IReadOnlyList<ProfileSummary>> ToSummariesAsync(IReadOnlyCollection<ProfileSummaryRow> rows,
        CancellationToken cancellationToken)
    {
        var profileRows = rows.ToArray();
        if (profileRows.Length == 0) return [];
        var ids = profileRows.Select(x => x.UserId).ToArray();
        var interests = await db.UserInterests.AsNoTracking().Where(x => ids.Contains(x.UserId))
            .OrderBy(x => x.Interest.Name).Select(x => new { x.UserId, x.Interest.Name })
            .ToListAsync(cancellationToken);
        var avatars = await db.Photos.AsNoTracking().Where(x => ids.Contains(x.UserId) && x.IsAvatar)
            .Select(x => new { x.UserId, x.Id }).ToDictionaryAsync(x => x.UserId,
                x => (string?)$"/api/v1/photos/{x.Id}/content", cancellationToken);
        var namesByUser = interests.GroupBy(x => x.UserId).ToDictionary(x => x.Key,
            x => (IReadOnlyCollection<string>)x.Select(v => v.Name).ToArray());
        return profileRows.Select(x => new ProfileSummary(x.UserId, x.DisplayName,
            Age(x.DateOfBirth), x.Bio, x.City,
            namesByUser.GetValueOrDefault(x.UserId, Array.Empty<string>()), avatars.GetValueOrDefault(x.UserId)))
            .ToArray();
    }

    private sealed record ProfileSummaryRow(Guid UserId, string DisplayName, string? Bio, string? City, DateOnly DateOfBirth);
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProfileValidationException(string message) : Exception(message);
public sealed class ProfileUnavailableException() : Exception("Hồ sơ không khả dụng.");
