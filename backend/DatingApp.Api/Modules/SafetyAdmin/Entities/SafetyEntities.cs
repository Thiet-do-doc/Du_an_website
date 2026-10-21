namespace DatingApp.Api.Modules.SafetyAdmin.Entities;

public enum ReportTargetType { User, Message, Post }
public enum ReportStatus { Pending, Reviewed, Dismissed, Actioned }

public sealed class UserReport
{
    public Guid Id { get; set; }
    public required string ReporterUserId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public required string TargetId { get; set; }
    public string? TargetUserId { get; set; }
    public string? Reason { get; set; }
    public required string Description { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}

public sealed class ModerationAction
{
    public Guid Id { get; set; }
    public Guid? ReportId { get; set; }
    public required string ActorUserId { get; set; }
    public required string TargetUserId { get; set; }
    public required string Action { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
