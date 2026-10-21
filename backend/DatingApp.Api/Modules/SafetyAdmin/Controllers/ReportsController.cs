using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.SafetyAdmin.DTOs;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(AppDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReportResponse>> Create(CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TargetType)) return BadRequest(new { message = "Target type không hợp lệ." });
        if (request.TargetType != ReportTargetType.User)
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { message = "Module sở hữu tin nhắn/bài viết chưa cung cấp kiểm tra quyền đối tượng báo cáo." });
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Unauthorized();
        string? targetUserId = null;
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
            Reason = request.Reason.Trim(), Description = request.Description.Trim()
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
