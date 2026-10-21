using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Profiles.Contracts;
using DatingApp.Api.Modules.Profiles.Entities;
using DatingApp.Api.Modules.DiscoveryMatching.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.Profiles.Services;

public sealed class ProfileService(AppDbContext db, TimeProvider timeProvider) : IDiscoveryProfileQuery
{
    public async Task<MyProfileResponse?> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null) return null;
        var profile = await db.Profiles.Include(x => x.UserInterests).ThenInclude(x => x.Interest)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        return await ToResponseAsync(user, profile, ct);
    }

    public async Task<PublicProfileResponse?> GetPublicAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, ct);
        if (user is null) return null;
        var profile = await db.Profiles.Include(x => x.UserInterests).ThenInclude(x => x.Interest)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IsDiscoverable, ct);
        if (profile is null) return null;
        var ownView = await ToResponseAsync(user, profile, ct);
        return new PublicProfileResponse(ownView.UserId, ownView.DisplayName, ownView.Age, ownView.Bio,
            ownView.City, ownView.Gender, ownView.Photos, ownView.Interests);
    }

    public async Task<MyProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
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
        var existingInterests = profile.UserInterests.ToDictionary(x => x.InterestId);
        var removedInterests = existingInterests.Values.Where(x => !ids.Contains(x.InterestId)).ToArray();
        foreach (var removed in removedInterests)
        {
            profile.UserInterests.Remove(removed);
            db.UserInterests.Remove(removed);
        }
        foreach (var id in ids)
            if (!existingInterests.ContainsKey(id))
                profile.UserInterests.Add(new UserInterest { UserId = userId, InterestId = id });
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
        if (!IsComplete(user, profile)) return null;
        var avatarUrl = await db.Photos.Where(x => x.UserId == userId && x.IsAvatar)
            .Select(x => (string?)$"/api/v1/photos/{x.Id}/content").SingleOrDefaultAsync(cancellationToken);
        return new ProfileSummary(userId, profile.DisplayName, Age(user.DateOfBirth), profile.City ?? "",
            null, profile.Bio, profile.UserInterests.Select(x => x.Interest.Name).ToArray(), avatarUrl, null);
    }

    public async Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed, cancellationToken);
        var profile = await db.Profiles.Include(x => x.UserInterests)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return user is not null && profile is not null
            && await db.Photos.AnyAsync(x => x.UserId == userId, cancellationToken)
            && IsComplete(user, profile);
    }

    private async Task<MyProfileResponse> ToResponseAsync(ApplicationUser user, Profile? profile, CancellationToken ct)
    {
        var photos = profile is null ? [] : await db.Photos.Where(x => x.UserId == user.Id).OrderBy(x => x.SortOrder)
            .Select(x => new PhotoResponse(x.Id, $"/api/v1/photos/{x.Id}/content", x.IsAvatar, x.SortOrder)).ToArrayAsync(ct);
        var interests = profile?.UserInterests.OrderBy(x => x.Interest.Name)
            .Select(x => new InterestResponse(x.InterestId, x.Interest.Name)).ToArray() ?? [];
        return new MyProfileResponse(user.Id, profile?.DisplayName ?? "", Age(user.DateOfBirth), profile?.Bio,
            profile?.City, profile?.Gender, profile?.IsDiscoverable ?? false,
            profile is not null && IsComplete(user, profile), photos, interests);
    }

    private bool IsComplete(ApplicationUser user, Profile profile) => !string.IsNullOrWhiteSpace(profile.DisplayName)
        && profile.UserInterests.Count > 0
        && user.EmailConfirmed && user.IsActive && Age(user.DateOfBirth) >= 18;
    private int Age(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return today.Year - dob.Year - (today < dob.AddYears(today.Year - dob.Year) ? 1 : 0);
    }
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProfileValidationException(string message) : Exception(message);
public sealed class ProfileUnavailableException() : Exception("Hồ sơ không khả dụng.");
