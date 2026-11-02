using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Profiles.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using DatingApp.Api.Shared;

namespace DatingApp.Api.Modules.Profiles.Services;

public sealed class PhotoService(AppDbContext db)
{
    public const int MaxPhotos = 6;

    public async Task<Photo> UploadAsync(Guid userId, Stream stream, string? contentType, CancellationToken ct)
    {
        var image = await UploadedImage.ReadAsync(stream, contentType, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var profile = await db.Profiles.FromSqlInterpolated($"SELECT * FROM \"Profiles\" WHERE \"UserId\" = {userId} FOR UPDATE")
            .Include(x => x.Photos).SingleOrDefaultAsync(ct);
        if (profile is null) throw new ProfileUnavailableException();
        if (profile.Photos.Count >= MaxPhotos) throw new PhotoValidationException("Mỗi hồ sơ có tối đa 6 ảnh.");
        var photo = new Photo { UserId = userId, Profile = profile, Content = image.Content, ContentType = image.ContentType,
            IsAvatar = profile.Photos.Count == 0, SortOrder = profile.Photos.Count };
        db.Photos.Add(photo);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return photo;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        var photo = await db.Photos.SingleOrDefaultAsync(x => x.Id == photoId && x.UserId == userId, ct);
        if (photo is null) return false;
        var wasAvatar = photo.IsAvatar;
        db.Photos.Remove(photo);
        await db.SaveChangesAsync(ct);
        if (wasAvatar)
        {
            var next = await db.Photos.Where(x => x.UserId == userId).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct);
            if (next is not null) { next.IsAvatar = true; await db.SaveChangesAsync(ct); }
        }
        var remaining = await db.Photos.Where(x => x.UserId == userId).OrderBy(x => x.SortOrder).ToListAsync(ct);
        for (var i = 0; i < remaining.Count; i++) remaining[i].SortOrder = i;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetAvatarAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        var photos = await db.Photos.Where(x => x.UserId == userId).ToListAsync(ct);
        var selected = photos.SingleOrDefault(x => x.Id == photoId);
        if (selected is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var photo in photos) photo.IsAvatar = false;
        await db.SaveChangesAsync(ct);
        selected.IsAvatar = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> ReorderAsync(Guid userId, IReadOnlyCollection<Guid> photoIds, CancellationToken ct)
    {
        var photos = await db.Photos.Where(x => x.UserId == userId).ToListAsync(ct);
        if (photoIds.Count != photos.Count || photoIds.Distinct().Count() != photos.Count
            || photos.Any(x => !photoIds.Contains(x.Id))) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var photo in photos) photo.SortOrder += MaxPhotos;
        await db.SaveChangesAsync(ct);
        for (var i = 0; i < photoIds.Count; i++) photos.Single(x => x.Id == photoIds.ElementAt(i)).SortOrder = i;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<Photo?> GetContentAsync(Guid photoId, Guid viewerId, CancellationToken ct) =>
        await db.Photos.Include(x => x.Profile).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == photoId && (x.UserId == viewerId || x.Profile.IsDiscoverable)
                && x.Profile.User.IsActive && x.Profile.User.EmailConfirmed, ct);
}
