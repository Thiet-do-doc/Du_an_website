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
[Authorize(Policy = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/admin")]
public sealed class AdminModerationController(
    AppDbContext db, IAccountAccess accountAccess, AuthConnectionRegistry connections) : ControllerBase
{
    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportResponse>>> ListReports(
        [FromQuery] ReportStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if ((long)(page - 1) * pageSize > int.MaxValue) return BadRequest();
        var query = db.Reports.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize)
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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var report = (await db.Reports.FromSqlInterpolated($"SELECT * FROM \"Reports\" WHERE \"Id\" = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (report is null) return NotFound();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (report.Status == request.Status && report.ReviewNotes == request.Notes.Trim() && report.ReviewedByUserId == actorId)
            return Ok(ReportsController.ToResponse(report));
        if (report.Status is ReportStatus.Dismissed or ReportStatus.Actioned)
            return Conflict(new { message = "Báo cáo đã được xử lý. Hãy tải lại." });
        if (report.TargetUserId is null)
            return Conflict(new { message = "Báo cáo thiếu tài khoản đích để ghi audit." });
        report.Status = request.Status;
        report.ReviewedByUserId = actorId;
        report.ReviewNotes = request.Notes.Trim();
        report.ReviewedAtUtc = DateTime.UtcNow;
        db.ModerationActions.Add(new ModerationAction
        {
            Id = Guid.NewGuid(), ReportId = report.Id, ActorUserId = actorId,
            TargetUserId = report.TargetUserId,
            Action = $"Report{request.Status}", Reason = request.Notes.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ReportsController.ToResponse(report));
    }

    [HttpGet("reports/{id:guid}/actions")]
    public async Task<ActionResult<IReadOnlyList<ModerationActionResponse>>> Actions(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Reports.AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
        return Ok(await db.ModerationActions.AsNoTracking().Where(x => x.ReportId == id)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new ModerationActionResponse(x.Id, x.ReportId, x.ActorUserId, x.TargetUserId,
                x.Action, x.Reason, x.CreatedAtUtc)).ToListAsync(cancellationToken));
    }

    [HttpPost("reports/{id:guid}/evidence")]
    public async Task<IActionResult> ReadMessageEvidence(Guid id, CancellationToken cancellationToken)
    {
        var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report?.TargetType != ReportTargetType.Message || !Guid.TryParse(report.TargetId, out var messageId)
            || !Guid.TryParse(report.TargetUserId, out var senderId)) return NotFound();
        var message = await db.Messages.AsNoTracking().Where(x => x.Id == messageId && x.SenderId == senderId)
            .Select(x => new { x.Id, x.Content }).SingleOrDefaultAsync(cancellationToken);
        if (message is null) return NotFound();
        db.ModerationActions.Add(new ModerationAction {
            Id = Guid.NewGuid(), ReportId = id, ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            TargetUserId = senderId.ToString(), Action = "MessageEvidenceRead", Reason = "Xem tin nhắn được báo cáo"
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(message);
    }

    [HttpPatch("users/{id}/status")]
    public async Task<ActionResult<ModerationActionResponse>> SetUserStatus(string id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var userId)) return BadRequest();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var reason = request.Reason.Trim();
        if (reason.Length < 5) return BadRequest(new { message = "Lý do cần ít nhất 5 ký tự." });
        if (request.ReportId is Guid reportId)
        {
            var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
            if (report is null) return NotFound();
            if (!Guid.TryParse(report.TargetUserId, out var target) || target != userId)
                return BadRequest(new { message = "Tài khoản không thuộc báo cáo này." });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (request.ReportId.HasValue)
        {
            var target = (await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken)).SingleOrDefault();
            if (target is null) return NotFound();
            if (target.IsActive == request.IsActive)
                return Conflict(new { message = "Tài khoản đã ở trạng thái được yêu cầu." });
        }
        if (!await accountAccess.SetActiveAsync(userId, request.IsActive, cancellationToken)) return NotFound();
        var action = new ModerationAction
        {
            Id = Guid.NewGuid(), ReportId = request.ReportId, ActorUserId = actorId, TargetUserId = userId.ToString(),
            Action = request.IsActive ? "AccountReactivated" : "AccountSuspended", Reason = reason
        };
        db.ModerationActions.Add(action);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        connections.Disconnect(userId);
        return Ok(new ModerationActionResponse(action.Id, action.ReportId, action.ActorUserId,
            action.TargetUserId, action.Action, action.Reason, action.CreatedAtUtc));
    }
}
