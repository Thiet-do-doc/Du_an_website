namespace DatingApp.Api.Modules.SafetyAdmin.Entities;

/// <summary>A one-way block owned by BlockerUserId and applied to BlockedUserId.</summary>
public sealed class UserBlock
{
    public Guid BlockerUserId { get; set; }
    public Guid BlockedUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
