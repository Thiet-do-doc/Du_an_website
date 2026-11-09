using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DatingApp.Api.Modules.Posts.Entities;
public sealed class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public string Content { get; set; } = "";
    public string? Feeling { get; set; }
    public byte[]? PhotoContent { get; set; }
    public string? PhotoType { get; set; }
    public ICollection<PostLike> Likes { get; } = new List<PostLike>();
    public ICollection<PostComment> Comments { get; } = new List<PostComment>();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> entity)
    {
        entity.ToTable("Posts");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Feeling).HasMaxLength(32);
        entity.Property(x => x.PhotoType).HasMaxLength(30);
        entity.HasIndex(x => new { x.CreatedAtUtc, x.Id });
        entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PostLike
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class PostComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentId { get; set; }
    public ICollection<CommentReaction> Reactions { get; } = new List<CommentReaction>();
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> entity)
    {
        entity.ToTable("PostLikes");
        entity.HasKey(x => new { x.PostId, x.UserId });
        entity.HasOne<Post>().WithMany(p => p.Likes).HasForeignKey(x => x.PostId);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
    }
}

public sealed class CommentReaction
{
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "like";
}

public sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> entity)
    {
        entity.ToTable("CommentReactions", t => t.HasCheckConstraint("CK_CommentReactions_Kind", "\"Kind\" IN ('like', 'haha', 'angry', 'sad')"));
        entity.HasKey(x => new { x.CommentId, x.UserId });
        entity.Property(x => x.Kind).HasMaxLength(12);
        entity.HasOne<PostComment>().WithMany(x => x.Reactions).HasForeignKey(x => x.CommentId);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
    }
}

public sealed class PostCommentConfiguration : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> entity)
    {
        entity.ToTable("PostComments");
        entity.Property(x => x.Content).HasMaxLength(1000);
        entity.HasIndex(x => new { x.PostId, x.CreatedAtUtc, x.Id });
        entity.HasOne<Post>().WithMany(p => p.Comments).HasForeignKey(x => x.PostId);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
        entity.HasOne<PostComment>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
    }
}
