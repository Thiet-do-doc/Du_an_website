using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.SafetyAdmin.DTOs;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.ChatNotifications.Services;
using DatingApp.Api.Modules.Posts.Entities;

namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/reports")]
public sealed class ReportsController(AppDbContext db, UserManager<ApplicationUser> users, IChatReportTargetAccess chatAccess) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery, System.ComponentModel.DataAnnotations.Range(1, 10000)] int page = 1, CancellationToken ct = default)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var rows = await db.Reports.AsNoTracking().Where(r => r.ReporterUserId == actor)
            .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).Skip((page - 1) * 20).Take(21)
            .Select(r => new { r.Id, r.TargetType, r.Reason, r.Description, r.Status, r.CreatedAtUtc }).ToListAsync(ct);
        return Ok(new { items = rows.Take(20), hasMore = rows.Count > 20 });
    }

    [HttpPost, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("reports")]
    public async Task<ActionResult<ReportResponse>> Create(CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TargetType)) return BadRequest(new { message = "Target type không hợp lệ." });
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Unauthorized();
        string? targetUserId = null;
        Post? post = null;
        if (request.Description.Trim().Length < 10 || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Chọn lý do và nhập mô tả ít nhất 10 ký tự." });
        if (request.TargetType == ReportTargetType.Post)
        {
            if (!Guid.TryParse(request.TargetId, out var postId)) return BadRequest();
            var viewer = Guid.Parse(actorId);
            post = await db.Set<Post>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == postId && !p.IsHidden &&
                p.Author.IsActive && p.Author.EmailConfirmed &&
                !db.Blocks.Any(b => b.BlockerUserId == viewer && b.BlockedUserId == p.AuthorId ||
                    b.BlockedUserId == viewer && b.BlockerUserId == p.AuthorId), cancellationToken);
            if (post is null) return NotFound();
            if (post.AuthorId == viewer) return BadRequest(new { message = "Bạn không thể báo cáo bài viết của mình." });
            targetUserId = post.AuthorId.ToString();
        }
        if (request.TargetType == ReportTargetType.Message)
        {
            if (!Guid.TryParse(actorId, out var reporterId) || !Guid.TryParse(request.TargetId, out var messageId)
                || !await chatAccess.CanReportMessageAsync(reporterId, messageId, cancellationToken)) return NotFound();
            targetUserId = (await db.Messages.Where(x => x.Id == messageId).Select(x => x.SenderId)
                .SingleAsync(cancellationToken)).ToString();
        }
        if (request.TargetType == ReportTargetType.User)
        {
            targetUserId = request.TargetId.Trim();
            if (targetUserId == actorId) return BadRequest(new { message = "Bạn không thể báo cáo chính mình." });
            if (!Guid.TryParse(targetUserId, out var targetGuid)) return BadRequest();
            if (await users.FindByIdAsync(targetGuid.ToString()) is null) return NotFound(new { message = "Không tìm thấy người dùng được báo cáo." });
        }
        var report = new UserReport
        {
            Id = Guid.NewGuid(), ReporterUserId = actorId, TargetType = request.TargetType,
            TargetId = request.TargetId.Trim(), TargetUserId = targetUserId,
            Reason = request.Reason.Trim(), Description = request.Description.Trim(),
            EvidenceContent = post?.Content, EvidenceFeeling = post?.Feeling,
            EvidencePhoto = post?.PhotoContent, EvidencePhotoType = post?.PhotoType
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/v1/admin/reports/{report.Id}", ToResponse(report));
    }

    internal static ReportResponse ToResponse(UserReport report) => new(
        report.Id, report.ReporterUserId, report.TargetType, report.TargetId, report.TargetUserId,
        report.Reason ?? "", report.Description, report.Status, report.ReviewedByUserId,
        report.ReviewNotes, report.CreatedAtUtc, report.ReviewedAtUtc);
}
