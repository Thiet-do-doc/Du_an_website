using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.DiscoveryMatching.DTOs;
using DatingApp.Api.Modules.DiscoveryMatching.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

public sealed class MatchService(AppDbContext db, IMatchConversationBridge conversations, TimeProvider clock) : IMatchService
{
    public async Task<LikeResponse> LikeAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        ValidatePair(fromUserId, toUserId);

        // PostgreSQL Serializable plus unique keys protects the pair when opposite likes race.
        // A unique-key loser retries against the committed winner and returns the same match.
        for (var attempt = 0; ; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var committed = false;
            try
            {
                var existingMatch = await FindPairAsync(fromUserId, toUserId, cancellationToken);
                if (existingMatch is { UnmatchedAt: not null }) throw new MatchUnavailableException();
                if (existingMatch is { UnmatchedAt: null })
                {
                    await tx.CommitAsync(cancellationToken);
                    committed = true;
                    var existingConversationId = await conversations.GetConversationIdsAsync([existingMatch.Id], cancellationToken);
                    await conversations.PublishMatchCreatedAsync(existingMatch.Id, cancellationToken);
                    return new(true, existingMatch.Id, existingConversationId[existingMatch.Id], existingMatch.MatchedAt);
                }

                if (!await db.UserLikes.AnyAsync(x => x.FromUserId == fromUserId && x.ToUserId == toUserId, cancellationToken))
                    db.UserLikes.Add(new UserLike { FromUserId = fromUserId, ToUserId = toUserId, CreatedAt = clock.GetUtcNow().UtcDateTime });
                await db.Passes.Where(x => x.FromUserId == fromUserId && x.ToUserId == toUserId).ExecuteDeleteAsync(cancellationToken);

                var mutualLike = await db.UserLikes.AnyAsync(x => x.FromUserId == toUserId && x.ToUserId == fromUserId, cancellationToken);
                if (!mutualLike)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return new(false, null, null, null);
                }

                var now = clock.GetUtcNow().UtcDateTime;
                var match = existingMatch ?? new Match
                {
                    Id = Guid.NewGuid(),
                    UserLowId = Min(fromUserId, toUserId),
                    UserHighId = Max(fromUserId, toUserId),
                    MatchedAt = now,
                };
                if (existingMatch is null) db.Matches.Add(match);
                else { existingMatch.UnmatchedAt = null; existingMatch.MatchedAt = now; }

                await db.SaveChangesAsync(cancellationToken);
                var conversationId = await conversations.EnsureConversationAsync(match.Id, cancellationToken);
                await db.UserLikes.Where(x =>
                    (x.FromUserId == fromUserId && x.ToUserId == toUserId) ||
                    (x.FromUserId == toUserId && x.ToUserId == fromUserId)).ExecuteDeleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                committed = true;

                // Realtime notification is deliberately outside the transaction. GET /matches is recovery.
                await conversations.PublishMatchCreatedAsync(match.Id, cancellationToken);
                return new(true, match.Id, conversationId, match.MatchedAt);
            }
            catch (Exception ex) when (!committed && IsRetryableConcurrencyFailure(ex) && attempt < 2)
            {
                // PostgreSQL can already have rolled back a serialization failure at COMMIT.
                try { await tx.RollbackAsync(cancellationToken); }
                catch (InvalidOperationException) { }
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task PassAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        ValidatePair(fromUserId, toUserId);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await db.Passes.AnyAsync(x => x.FromUserId == fromUserId && x.ToUserId == toUserId, cancellationToken))
                db.Passes.Add(new Pass { FromUserId = fromUserId, ToUserId = toUserId, CreatedAt = clock.GetUtcNow().UtcDateTime });
            await db.UserLikes.Where(x => x.FromUserId == fromUserId && x.ToUserId == toUserId).ExecuteDeleteAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent identical pass may win the composite-key insert.
            await tx.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            if (!await db.Passes.AnyAsync(x => x.FromUserId == fromUserId && x.ToUserId == toUserId, cancellationToken)) throw;
        }
    }

    public async Task<IReadOnlyList<MatchResponse>> GetMatchesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var matches = await db.Matches.AsNoTracking()
            .Where(x => x.UnmatchedAt == null && (x.UserLowId == userId || x.UserHighId == userId))
            .OrderByDescending(x => x.MatchedAt)
            .Select(x => new { x.Id, OtherUserId = x.UserLowId == userId ? x.UserHighId : x.UserLowId, x.MatchedAt })
            .ToListAsync(cancellationToken);
        var conversationsByMatch = await conversations.GetConversationIdsAsync(matches.Select(x => x.Id).ToArray(), cancellationToken);
        return matches.Select(x => new MatchResponse(x.Id, x.OtherUserId, conversationsByMatch[x.Id], x.MatchedAt, true)).ToArray();
    }

    public async Task<bool> UnmatchAsync(Guid userId, Guid matchId, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var match = await db.Matches.SingleOrDefaultAsync(x => x.Id == matchId && (x.UserLowId == userId || x.UserHighId == userId), cancellationToken);
        if (match is null) { await tx.CommitAsync(cancellationToken); return false; }
        if (match.UnmatchedAt is null)
        {
            match.UnmatchedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            await db.UserLikes.Where(x =>
                (x.FromUserId == match.UserLowId && x.ToUserId == match.UserHighId) ||
                (x.FromUserId == match.UserHighId && x.ToUserId == match.UserLowId)).ExecuteDeleteAsync(cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);
        // Idempotent retry can re-publish if the first post-commit notification was missed.
        await conversations.MatchEndedAsync(matchId, cancellationToken);
        return true;
    }

    private Task<Match?> FindPairAsync(Guid a, Guid b, CancellationToken ct) =>
        db.Matches.SingleOrDefaultAsync(x => x.UserLowId == Min(a, b) && x.UserHighId == Max(a, b), ct);

    private static bool IsRetryableConcurrencyFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }

    private static void ValidatePair(Guid from, Guid to)
    {
        if (from == Guid.Empty || to == Guid.Empty) throw new ArgumentException("UserId phải là UUID hợp lệ.");
        if (from == to) throw new ArgumentException("Không thể tương tác với chính mình.");
    }

    private static Guid Min(Guid a, Guid b) => a.CompareTo(b) < 0 ? a : b;
    private static Guid Max(Guid a, Guid b) => a.CompareTo(b) > 0 ? a : b;
}

public sealed class MatchUnavailableException() : Exception("P0 không cho phép ghép lại sau khi đã hủy Match.");
