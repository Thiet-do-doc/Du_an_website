using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.SafetyAdmin.DTOs;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminModerationController(
    AppDbContext db, IAccountStatusService accountStatus) : ControllerBase
{
    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportResponse>>> ListReports(
        [FromQuery] ReportStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Reports.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderBy(x => x.CreatedAtUtc).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(x => new ReportResponse(x.Id, x.ReporterUserId,
                x.TargetType, x.TargetId, x.TargetUserId, x.Reason ?? "", x.Description,
                x.Status, x.ReviewedByUserId, x.ReviewNotes, x.CreatedAtUtc, x.ReviewedAtUtc))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("reports/{id:guid}")]
    public async Task<ActionResult<ReportResponse>> GetReport(Guid id, CancellationToken cancellationToken)
    {
        var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return report is null ? NotFound() : Ok(ReportsController.ToResponse(report));
    }

    [HttpPatch("reports/{id:guid}")]
    public async Task<ActionResult<ReportResponse>> UpdateReport(Guid id, UpdateReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status) || request.Status == ReportStatus.Pending)
            return BadRequest(new { message = "Trạng thái xử lý không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.Notes))
            return BadRequest(new { message = "Cần ghi lý do xử lý." });
        var report = await db.Reports.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null) return NotFound();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        report.Status = request.Status;
        report.ReviewedByUserId = actorId;
        report.ReviewNotes = request.Notes.Trim();
        report.ReviewedAtUtc = DateTime.UtcNow;
        db.ModerationActions.Add(new ModerationAction
        {
            Id = Guid.NewGuid(), ReportId = report.Id, ActorUserId = actorId,
            TargetUserId = report.TargetUserId ?? report.ReporterUserId,
            Action = $"Report{request.Status}", Reason = request.Notes.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ReportsController.ToResponse(report));
    }

    [HttpPatch("users/{id}/status")]
    public async Task<ActionResult<ModerationActionResponse>> SetUserStatus(string id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var userId)) return BadRequest();
        var exists = await db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (!exists) return NotFound();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var reason = request.Reason.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var status = await accountStatus.SetActiveAsync(id, request.IsActive, cancellationToken);
        if (!status.Found) return NotFound();
        if (!status.Succeeded) return BadRequest(new { errors = status.Errors });
        var action = new ModerationAction
        {
            Id = Guid.NewGuid(), ActorUserId = actorId, TargetUserId = id,
            Action = request.IsActive ? "AccountReactivated" : "AccountSuspended", Reason = reason
        };
        db.ModerationActions.Add(action);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new ModerationActionResponse(action.Id, action.ReportId, action.ActorUserId,
            action.TargetUserId, action.Action, action.Reason, action.CreatedAtUtc));
    }
}
