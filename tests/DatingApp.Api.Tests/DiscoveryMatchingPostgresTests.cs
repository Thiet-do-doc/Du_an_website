using System.Collections.Concurrent;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using DatingApp.Api.Modules.Profiles.Entities;
using DatingApp.Api.Modules.Profiles.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed class DiscoveryMatchingPostgresTests
{
    [Fact]
    public async Task Self_like_is_rejected_before_database_access()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_used;Username=not_used;Password=not_used")
            .Options;
        await using var db = new AppDbContext(options);
        var service = new MatchService(db, new RecordingConversationBridge(), TimeProvider.System);
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => service.LikeAsync(userId, userId, default));
    }

    [PostgresFact]
    public async Task Opposite_likes_are_idempotent_and_concurrent_match_is_unique()
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTH_TEST_CONNECTION")!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var userIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var bridge = new RecordingConversationBridge();

        await using (var setup = new AppDbContext(options))
        {
            setup.Users.AddRange(userIds.Select(id => CreateUser(id)));
            await setup.SaveChangesAsync();
        }

        try
        {
            var first = await Like(userIds[0], userIds[1]);
            var repeat = await Like(userIds[0], userIds[1]);
            Assert.False(first.IsMatch);
            Assert.False(repeat.IsMatch);
            var reciprocal = await Like(userIds[1], userIds[0]);
            Assert.True(reciprocal.IsMatch);

            await using (var pairDb = new AppDbContext(options))
            {
                var pairAB = new[] { userIds[0], userIds[1] };
                var pairMatch = await pairDb.Matches.SingleAsync(x => pairAB.Contains(x.UserLowId) && pairAB.Contains(x.UserHighId));
                Assert.False(await new MatchService(pairDb, bridge, TimeProvider.System)
                    .UnmatchAsync(userIds[2], pairMatch.Id, default));
                Assert.True(await new MatchService(pairDb, bridge, TimeProvider.System)
                    .UnmatchAsync(userIds[0], pairMatch.Id, default));
            }
            await Assert.ThrowsAsync<MatchUnavailableException>(() => Like(userIds[0], userIds[1]));

            var opposite = await Task.WhenAll(
                Like(userIds[2], userIds[3]),
                Like(userIds[3], userIds[2]));
            Assert.Single(opposite, x => x.IsMatch);

            await using var verify = new AppDbContext(options);
            var pair = new[] { userIds[2], userIds[3] };
            var match = await verify.Matches.SingleAsync(x => pair.Contains(x.UserLowId) && pair.Contains(x.UserHighId));
            Assert.Null(match.UnmatchedAt);
            Assert.Equal(1, await verify.Matches.CountAsync(x => pair.Contains(x.UserLowId) && pair.Contains(x.UserHighId)));
            Assert.Equal(2, bridge.EnsuredConversationCount);
            Assert.Equal(0, await verify.UserLikes.CountAsync(x => pair.Contains(x.FromUserId) && pair.Contains(x.ToUserId)));
            Assert.False(await new MatchService(verify, bridge, TimeProvider.System)
                .UnmatchAsync(Guid.NewGuid(), match.Id, default));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Matches.Where(x => userIds.Contains(x.UserLowId) || userIds.Contains(x.UserHighId)).ExecuteDeleteAsync();
            await cleanup.UserLikes.Where(x => userIds.Contains(x.FromUserId) || userIds.Contains(x.ToUserId)).ExecuteDeleteAsync();
            await cleanup.Passes.Where(x => userIds.Contains(x.FromUserId) || userIds.Contains(x.ToUserId)).ExecuteDeleteAsync();
            await cleanup.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        async Task<DatingApp.Api.Modules.DiscoveryMatching.DTOs.LikeResponse> Like(Guid from, Guid to)
        {
            await using var db = new AppDbContext(options);
            return await new MatchService(db, bridge, TimeProvider.System).LikeAsync(from, to, default);
        }
    }

    [PostgresFact]
    public async Task Discovery_omits_hidden_locked_incomplete_and_blocked_profiles()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("AUTH_TEST_CONNECTION")!).Options;
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var viewer = ids[0];
        var visible = ids[1];
        var hidden = ids[2];
        var locked = ids[3];
        var incomplete = ids[4];
        var blocked = ids[5];
        var wrongFilters = ids[6];
        var noPhoto = ids[7];
        var interestId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        await using (var setup = new AppDbContext(options))
        {
            await SeedProfile(setup, viewer, "Viewer", active: true, discoverable: true, complete: true, interestId: interestId);
            await SeedProfile(setup, visible, "Visible", active: true, discoverable: true, complete: true,
                interestId: interestId, dateOfBirth: today.AddYears(-30));
            await SeedProfile(setup, hidden, "Hidden", active: true, discoverable: false, complete: true, interestId: interestId);
            await SeedProfile(setup, locked, "Locked", active: false, discoverable: true, complete: true, interestId: interestId);
            await SeedProfile(setup, incomplete, "Incomplete", active: true, discoverable: true, complete: false, interestId: interestId);
            await SeedProfile(setup, blocked, "Blocked", active: true, discoverable: true, complete: true, interestId: interestId);
            await SeedProfile(setup, wrongFilters, "Wrong filters", active: true, discoverable: true,
                complete: true, interestId: Guid.Parse("10000000-0000-0000-0000-000000000002"),
                dateOfBirth: today.AddYears(-45), city: "TP. Hồ Chí Minh");
            await SeedProfile(setup, noPhoto, "No photo", active: true, discoverable: true, complete: true,
                interestId: interestId, dateOfBirth: today.AddYears(-30), includePhoto: false);
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var db = new AppDbContext(options);
            var profiles = new ProfileService(db, TimeProvider.System);
            var service = new DiscoveryService(db, new DiscoveryProfileQueryAdapter(profiles), new FixedBlockLookup(blocked));
            var result = await service.SearchAsync(viewer, new DatingApp.Api.Modules.DiscoveryMatching.DTOs.DiscoveryQuery(), default);
            Assert.Equal(new[] { noPhoto, visible }, result.Items.Select(x => x.UserId));
            var filtered = await service.SearchAsync(viewer, new DatingApp.Api.Modules.DiscoveryMatching.DTOs.DiscoveryQuery(
                MinAge: 30, MaxAge: 30, City: "Hà Nội", Interest: "Du lịch"), default);
            Assert.Equal(new[] { noPhoto, visible }, filtered.Items.Select(x => x.UserId));
            Assert.All(filtered.Items, item =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize(item);
                Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("dateofbirth", json, StringComparison.OrdinalIgnoreCase);
            });
            await Assert.ThrowsAsync<ProfileNotCompleteException>(() => service.SearchAsync(incomplete,
                new DatingApp.Api.Modules.DiscoveryMatching.DTOs.DiscoveryQuery(), default));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Profiles.Where(x => ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await cleanup.Users.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }

    private static ApplicationUser CreateUser(Guid id, DateOnly? dateOfBirth = null)
    {
        var email = $"matching-{id:N}@example.test";
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            DateOfBirth = dateOfBirth ?? new DateOnly(1995, 1, 1),
        };
    }

    private static async Task SeedProfile(AppDbContext db, Guid id, string name, bool active, bool discoverable,
        bool complete, Guid interestId, DateOnly? dateOfBirth = null, string city = "Hà Nội", bool includePhoto = true)
    {
        var user = CreateUser(id, dateOfBirth);
        user.IsActive = active;
        var profile = new Profile
        {
            UserId = id,
            User = user,
            DisplayName = name,
            City = city,
            IsDiscoverable = discoverable,
        };
        db.Users.Add(user);
        db.Profiles.Add(profile);
        if (complete)
        {
            if (includePhoto)
                db.Photos.Add(new Photo { UserId = id, Profile = profile, ContentType = "image/jpeg", IsAvatar = true });
            db.UserInterests.Add(new UserInterest { UserId = id, Profile = profile, InterestId = interestId });
        }
        await Task.CompletedTask;
    }

    private sealed class RecordingConversationBridge : IMatchConversationBridge
    {
        private readonly ConcurrentDictionary<Guid, Guid> _ids = new();
        public int EnsuredConversationCount => _ids.Count;
        public Task<Guid> EnsureConversationAsync(Guid matchId, CancellationToken cancellationToken) =>
            Task.FromResult(_ids.GetOrAdd(matchId, _ => Guid.NewGuid()));
        public Task<IReadOnlyDictionary<Guid, Guid>> GetConversationIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(_ids.Where(x => matchIds.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value));
        public Task PublishMatchCreatedAsync(Guid matchId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MatchEndedAsync(Guid matchId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedBlockLookup(Guid blockedUserId) : IBlockLookup
    {
        public Task<IReadOnlySet<Guid>> GetBlockedUserIdsAsync(Guid viewerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(viewerId == Guid.Empty ? new HashSet<Guid>() : new HashSet<Guid> { blockedUserId });
    }
}
