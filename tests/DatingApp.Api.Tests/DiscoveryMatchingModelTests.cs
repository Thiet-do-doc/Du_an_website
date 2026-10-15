using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.DiscoveryMatching.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed class DiscoveryMatchingModelTests
{
    [Fact]
    public void Like_and_pass_use_pair_keys_and_forbid_self_interaction()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var like = model.FindEntityType(typeof(UserLike))!;
        var pass = model.FindEntityType(typeof(Pass))!;

        Assert.Equal(new[] { nameof(UserLike.FromUserId), nameof(UserLike.ToUserId) }, like.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(new[] { nameof(Pass.FromUserId), nameof(Pass.ToUserId) }, pass.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Contains(like.GetCheckConstraints(), x => x.Name == "CK_UserLikes_NoSelfLike");
        Assert.Contains(pass.GetCheckConstraints(), x => x.Name == "CK_Passes_NoSelfPass");
    }

    [Fact]
    public void Match_pair_is_ordered_and_unique()
    {
        using var db = CreateContext();
        var match = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Match))!;
        var pairIndex = Assert.Single(match.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(Match.UserLowId), nameof(Match.UserHighId) }));

        Assert.True(pairIndex.IsUnique);
        Assert.Contains(match.GetCheckConstraints(), x => x.Name == "CK_Matches_UserOrder");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        return new AppDbContext(options);
    }
}
