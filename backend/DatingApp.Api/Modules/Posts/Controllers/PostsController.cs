using System.ComponentModel.DataAnnotations;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.Posts.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DatingApp.Api.Modules.Posts.Controllers;

public sealed record CreatePostRequest([Required, StringLength(2000, MinimumLength = 1)] string Content);
public sealed record PostResponse(Guid Id, Guid AuthorId, string AuthorName, string Content, DateTimeOffset CreatedAtUtc, bool IsMine);

[ApiController, Authorize(Policy = "User"), Route("api/v1/posts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PostsController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Feed([FromQuery, Range(1, 10000)] int page = 1, CancellationToken ct = default)
    {
        var viewer = currentUser.UserId!.Value;
        // Blocks in either direction and deactivated accounts never appear in the feed.
        var rows = await (from post in db.Set<Post>().AsNoTracking()
            join profile in db.Profiles on post.AuthorId equals profile.UserId
            where post.Author.IsActive && post.Author.EmailConfirmed
                && !db.Blocks.Any(b => b.BlockerUserId == viewer && b.BlockedUserId == post.AuthorId
                    || b.BlockedUserId == viewer && b.BlockerUserId == post.AuthorId)
            orderby post.CreatedAtUtc descending, post.Id descending
            select new PostResponse(post.Id, post.AuthorId, profile.DisplayName, post.Content, post.CreatedAtUtc, post.AuthorId == viewer))
            .Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        return Ok(new { items = rows.Take(20), hasMore = rows.Count > 20 });
    }
    [HttpPost]
    public async Task<IActionResult> Create(CreatePostRequest request, CancellationToken ct)
    {
        var author = currentUser.UserId!.Value;
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            ModelState.AddModelError("content", "Hãy viết điều bạn muốn chia sẻ.");
            return ValidationProblem(ModelState);
        }
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == author, ct);
        if (profile is null || string.IsNullOrWhiteSpace(profile.DisplayName))
            return Conflict(new ProblemDetails { Status = 409, Title = "Hãy lưu tên hiển thị trong Hồ sơ trước khi đăng bài." });
        var post = new Post { AuthorId = author, Content = request.Content.Trim() };
        db.Set<Post>().Add(post);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new PostResponse(post.Id, author, profile.DisplayName, post.Content, post.CreatedAtUtc, true));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var post = await db.Set<Post>().SingleOrDefaultAsync(p => p.Id == id && p.AuthorId == currentUser.UserId, ct);
        if (post is null) return NotFound();
        db.Remove(post);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
