using System.ComponentModel.DataAnnotations;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.Posts.Entities;
using DatingApp.Api.Modules.ChatNotifications.Services;
using DatingApp.Api.Shared;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.Posts.Controllers;

public sealed record CreatePostRequest([StringLength(2000)] string? Content, [StringLength(32)] string? Feeling = null);
public sealed record CreateCommentRequest([Required, StringLength(1000)] string Content, Guid? ParentId = null);
public sealed record ReactCommentRequest([Required] string Kind);
public sealed record PostResponse(Guid Id, Guid AuthorId, string AuthorName, string Content, DateTimeOffset CreatedAtUtc,
    bool IsMine, string? Feeling, bool HasPhoto, int LikeCount, bool IsLiked, int CommentCount,
    bool CanOpenAuthorProfile, Guid? AuthorAvatarId);

[ApiController, Authorize(Policy = "User"), Route("api/v1/posts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PostsController(AppDbContext db, ICurrentUser currentUser, NotificationService notifications) : ControllerBase
{
    private Guid Viewer => currentUser.UserId!.Value;
    private IQueryable<Guid> VisiblePeople()
    {
        var viewer = Viewer;
        return db.Users.Where(u => u.IsActive && u.EmailConfirmed &&
            !db.Blocks.Any(b => b.BlockerUserId == viewer && b.BlockedUserId == u.Id ||
                b.BlockedUserId == viewer && b.BlockerUserId == u.Id)).Select(u => u.Id);
    }

    private IQueryable<Post> VisiblePosts()
    {
        var people = VisiblePeople();
        return db.Set<Post>().AsNoTracking().Where(p => people.Contains(p.AuthorId));
    }

    private IQueryable<PostResponse> Summaries(IQueryable<Post> posts)
    {
        var people = VisiblePeople();
        var viewer = Viewer;
        return from p in posts
               join profile in db.Profiles on p.AuthorId equals profile.UserId
               orderby p.CreatedAtUtc descending, p.Id descending
               select new PostResponse(p.Id, p.AuthorId, profile.DisplayName, p.Content, p.CreatedAtUtc,
                   p.AuthorId == viewer, p.Feeling, p.PhotoType != null,
                   p.Likes.Count(x => people.Contains(x.UserId)), p.Likes.Any(x => x.UserId == viewer),
                   p.Comments.Count(x => people.Contains(x.UserId)),
                   profile.IsDiscoverable || p.AuthorId == viewer,
                   profile.Photos.Where(x => x.IsAvatar && (profile.IsDiscoverable || p.AuthorId == viewer))
                       .Select(x => (Guid?)x.Id).FirstOrDefault());
    }

    [HttpGet]
    public async Task<IActionResult> Feed([FromQuery, Range(1, 10000)] int page = 1, CancellationToken ct = default)
    {
        var rows = await Summaries(VisiblePosts()).Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        return Ok(new { items = rows.Take(20), hasMore = rows.Count > 20 });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPost(Guid id, CancellationToken ct)
    {
        var post = await Summaries(VisiblePosts().Where(p => p.Id == id)).SingleOrDefaultAsync(ct);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var photo = await VisiblePosts().Where(p => p.Id == id && p.PhotoType != null)
            .Select(p => new { p.PhotoContent, p.PhotoType }).SingleOrDefaultAsync(ct);
        return photo is null ? NotFound() : File(photo.PhotoContent!, photo.PhotoType!);
    }

    [HttpPost, Consumes("application/json")]
    public Task<IActionResult> Create(CreatePostRequest request, CancellationToken ct) =>
        SavePost(request.Content, request.Feeling, null, ct);

    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> Upload([FromForm, StringLength(2000)] string? content,
        [FromForm, StringLength(32)] string? feeling, IFormFile? file, CancellationToken ct) =>
        SavePost(content, feeling, file, ct);

    private async Task<IActionResult> SavePost(string? text, string? feeling, IFormFile? file, CancellationToken ct)
    {
        var content = text?.Trim() ?? "";
        if (feeling is not (null or "Vui vẻ" or "Hạnh phúc" or "Biết ơn" or "Hào hứng" or "Buồn" or "Bình yên"))
            return Problem(statusCode: 400, title: "Cảm xúc không hợp lệ.");
        if (content.Length == 0 && feeling is null && file is null)
            return Problem(statusCode: 400, title: "Hãy viết nội dung, chọn cảm xúc hoặc thêm ảnh.");
        var author = Viewer;
        var name = await db.Profiles.Where(p => p.UserId == author).Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(name))
            return Problem(statusCode: 409, title: "Hãy lưu tên hiển thị trong Hồ sơ trước khi đăng bài.");
        var post = new Post { AuthorId = author, Content = content, Feeling = feeling };
        if (file is not null)
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var image = await UploadedImage.ReadAsync(stream, file.ContentType, ct);
                post.PhotoContent = image.Content;
                post.PhotoType = image.ContentType;
            }
            catch (PhotoValidationException e) { return Problem(statusCode: 400, title: e.Message); }
            catch (MagickException) { return Problem(statusCode: 400, title: "Tệp ảnh không hợp lệ."); }
        }
        db.Add(post);
        var people = VisiblePeople();
        var friends = await db.Matches.Where(m => m.UnmatchedAt == null && (m.UserLowId == author || m.UserHighId == author))
            .Select(m => m.UserLowId == author ? m.UserHighId : m.UserLowId).Where(id => people.Contains(id)).Distinct().ToListAsync(ct);
        foreach (var friend in friends)
            notifications.QueueSocial(friend, author, "friendPost", post.Id, name + " vừa đăng một bài viết mới.");
        await db.SaveChangesAsync(ct);
        return StatusCode(201, await Summaries(VisiblePosts().Where(p => p.Id == post.Id)).SingleAsync(ct));
    }

    [HttpPut("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, CancellationToken ct)
    {
        var post = await VisiblePosts().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var added = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"PostLikes\" (\"PostId\", \"UserId\") SELECT \"Id\", {Viewer} FROM \"Posts\" WHERE \"Id\" = {id} ON CONFLICT DO NOTHING", ct);
        if (added > 0)
        {
            var name = await db.Profiles.Where(p => p.UserId == Viewer).Select(p => p.DisplayName).SingleOrDefaultAsync(ct) ?? "Một thành viên";
            notifications.QueueSocial(post.AuthorId, Viewer, "postLike", id, name + " đã thích bài viết của bạn.");
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/like")]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken ct)
    {
        if (!await VisiblePosts().AnyAsync(p => p.Id == id, ct)) return NotFound();
        var viewer = Viewer;
        await db.Set<PostLike>().Where(l => l.PostId == id && l.UserId == viewer).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id, [FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery] Guid? parentId = null, CancellationToken ct = default)
    {
        var visiblePosts = VisiblePosts();
        if (!await visiblePosts.AnyAsync(p => p.Id == id, ct)) return NotFound();
        var people = VisiblePeople();
        if (parentId is not null && !await db.Set<PostComment>().AnyAsync(c => c.Id == parentId && c.PostId == id && people.Contains(c.UserId), ct)) return NotFound();
        var rows = await (from c in db.Set<PostComment>()
            join post in visiblePosts on c.PostId equals post.Id
            join profile in db.Profiles on c.UserId equals profile.UserId
            where c.PostId == id && c.ParentId == parentId && people.Contains(c.UserId)
            orderby c.CreatedAtUtc descending, c.Id descending
            select new { c.Id, authorId = c.UserId, authorName = profile.DisplayName, c.Content, c.CreatedAtUtc, c.ParentId,
                replyCount = db.Set<PostComment>().Count(r => r.ParentId == c.Id && people.Contains(r.UserId)),
                myReaction = c.Reactions.Where(r => r.UserId == Viewer).Select(r => r.Kind).FirstOrDefault(),
                reactions = new {
                    like = c.Reactions.Count(r => r.Kind == "like" && people.Contains(r.UserId)),
                    haha = c.Reactions.Count(r => r.Kind == "haha" && people.Contains(r.UserId)),
                    angry = c.Reactions.Count(r => r.Kind == "angry" && people.Contains(r.UserId)),
                    sad = c.Reactions.Count(r => r.Kind == "sad" && people.Contains(r.UserId))
                } })
            .Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        return Ok(new { items = rows.Take(20), hasMore = rows.Count > 20 });
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, CreateCommentRequest request, CancellationToken ct)
    {
        var post = await VisiblePosts().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();
        PostComment? parent = null;
        if (request.ParentId is not null)
        {
            var people = VisiblePeople();
            parent = await db.Set<PostComment>().SingleOrDefaultAsync(c => c.Id == request.ParentId && c.PostId == id && people.Contains(c.UserId), ct);
            if (parent is null) return NotFound();
        }
        if (string.IsNullOrWhiteSpace(request.Content)) return Problem(statusCode: 400, title: "Nhập nội dung bình luận.");
        var author = Viewer;
        var name = await db.Profiles.Where(p => p.UserId == author).Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(name)) return Problem(statusCode: 409, title: "Lưu tên hiển thị trong Hồ sơ để bình luận.");
        var comment = new PostComment { PostId = id, UserId = author, Content = request.Content.Trim(), ParentId = parent?.Id };
        db.Add(comment);
        if (parent?.UserId == post.AuthorId)
            notifications.QueueSocial(post.AuthorId, author, "reply", id, name + " đã trả lời bình luận của bạn.");
        else
            notifications.QueueSocial(post.AuthorId, author, "comment", id, name + " đã bình luận vào bài viết của bạn.");
        if (parent is not null && parent.UserId != post.AuthorId)
            notifications.QueueSocial(parent.UserId, author, "reply", id, name + " đã trả lời bình luận của bạn.");
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { comment.Id, authorId = author, authorName = name, comment.Content, comment.CreatedAtUtc, comment.ParentId });
    }

    [HttpPut("{id:guid}/comments/{commentId:guid}/reaction")]
    public async Task<IActionResult> React(Guid id, Guid commentId, ReactCommentRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("like" or "haha" or "angry" or "sad"))
            return Problem(statusCode: 400, title: "Cảm xúc không hợp lệ.");
        if (!await CanSeeComment(id, commentId, ct)) return NotFound();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommentReactions\" (\"CommentId\", \"UserId\", \"Kind\") VALUES ({commentId}, {Viewer}, {request.Kind}) ON CONFLICT (\"CommentId\", \"UserId\") DO UPDATE SET \"Kind\" = EXCLUDED.\"Kind\"", ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}/reaction")]
    public async Task<IActionResult> RemoveReaction(Guid id, Guid commentId, CancellationToken ct)
    {
        if (!await CanSeeComment(id, commentId, ct)) return NotFound();
        await db.Set<CommentReaction>().Where(r => r.CommentId == commentId && r.UserId == Viewer).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    private Task<bool> CanSeeComment(Guid postId, Guid commentId, CancellationToken ct)
    {
        var people = VisiblePeople();
        var posts = VisiblePosts();
        return db.Set<PostComment>().AnyAsync(c => c.Id == commentId && c.PostId == postId &&
            people.Contains(c.UserId) && posts.Any(p => p.Id == postId), ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var author = Viewer;
        var removed = await db.Set<Post>().Where(p => p.Id == id && p.AuthorId == author).ExecuteDeleteAsync(ct);
        return removed == 0 ? NotFound() : NoContent();
    }
}
