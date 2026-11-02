using System.ComponentModel.DataAnnotations;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.Posts.Entities;
using DatingApp.Api.Shared;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.Posts.Controllers;

public sealed record CreatePostRequest([StringLength(2000)] string? Content, [StringLength(32)] string? Feeling = null);
public sealed record CreateCommentRequest([Required, StringLength(1000)] string Content);
public sealed record PostResponse(Guid Id, Guid AuthorId, string AuthorName, string Content, DateTimeOffset CreatedAtUtc,
    bool IsMine, string? Feeling, bool HasPhoto, int LikeCount, bool IsLiked, int CommentCount);

[ApiController, Authorize(Policy = "User"), Route("api/v1/posts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PostsController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
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
                   p.Comments.Count(x => people.Contains(x.UserId)));
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
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new PostResponse(post.Id, author, name, content, post.CreatedAtUtc, true,
            feeling, post.PhotoType != null, 0, false, 0));
    }

    [HttpPut("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, CancellationToken ct)
    {
        if (!await VisiblePosts().AnyAsync(p => p.Id == id, ct)) return NotFound();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"PostLikes\" (\"PostId\", \"UserId\") SELECT \"Id\", {Viewer} FROM \"Posts\" WHERE \"Id\" = {id} ON CONFLICT DO NOTHING", ct);
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
    public async Task<IActionResult> Comments(Guid id, [FromQuery, Range(1, 10000)] int page = 1, CancellationToken ct = default)
    {
        var visiblePosts = VisiblePosts();
        if (!await visiblePosts.AnyAsync(p => p.Id == id, ct)) return NotFound();
        var people = VisiblePeople();
        var rows = await (from c in db.Set<PostComment>()
            join post in visiblePosts on c.PostId equals post.Id
            join profile in db.Profiles on c.UserId equals profile.UserId
            where c.PostId == id && people.Contains(c.UserId)
            orderby c.CreatedAtUtc descending, c.Id descending
            select new { c.Id, authorName = profile.DisplayName, c.Content, c.CreatedAtUtc })
            .Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        return Ok(new { items = rows.Take(20), hasMore = rows.Count > 20 });
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, CreateCommentRequest request, CancellationToken ct)
    {
        if (!await VisiblePosts().AnyAsync(p => p.Id == id, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Content)) return Problem(statusCode: 400, title: "Nhập nội dung bình luận.");
        var author = Viewer;
        var name = await db.Profiles.Where(p => p.UserId == author).Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(name)) return Problem(statusCode: 409, title: "Lưu tên hiển thị trong Hồ sơ để bình luận.");
        var comment = new PostComment { PostId = id, UserId = author, Content = request.Content.Trim() };
        db.Add(comment);
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { comment.Id, authorName = name, comment.Content, comment.CreatedAtUtc });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var author = Viewer;
        var removed = await db.Set<Post>().Where(p => p.Id == id && p.AuthorId == author).ExecuteDeleteAsync(ct);
        return removed == 0 ? NotFound() : NoContent();
    }
}
