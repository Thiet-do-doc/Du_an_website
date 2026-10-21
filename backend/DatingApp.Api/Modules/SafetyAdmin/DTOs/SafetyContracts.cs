using System.ComponentModel.DataAnnotations;
using DatingApp.Api.Modules.SafetyAdmin.Entities;

namespace DatingApp.Api.Modules.SafetyAdmin.DTOs;

public sealed record CreateReportRequest(
    [Required] ReportTargetType TargetType,
    [Required, MaxLength(200)] string TargetId,
    [Required, MaxLength(100)] string Reason,
    [Required, MinLength(10), MaxLength(2000)] string Description);

public sealed record UpdateReportRequest(
    [Required] ReportStatus Status,
    [MaxLength(2000)] string? Notes);

public sealed record UpdateUserStatusRequest(
    bool IsActive,
    [Required, MinLength(5), MaxLength(500)] string Reason);

public sealed record ReportResponse(
    Guid Id, string ReporterUserId, ReportTargetType TargetType, string TargetId,
    string? TargetUserId, string Reason, string Description, ReportStatus Status,
    string? ReviewedByUserId, string? ReviewNotes, DateTime CreatedAtUtc, DateTime? ReviewedAtUtc);

public sealed record ModerationActionResponse(
    Guid Id, Guid? ReportId, string ActorUserId, string TargetUserId,
    string Action, string Reason, DateTime CreatedAtUtc);
