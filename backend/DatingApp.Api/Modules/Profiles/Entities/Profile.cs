using DatingApp.Api.Modules.Auth.Entities;

namespace DatingApp.Api.Modules.Profiles.Entities;

public sealed class Profile
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? Gender { get; set; }
    public bool IsDiscoverable { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<UserInterest> UserInterests { get; set; } = new List<UserInterest>();
}

public sealed class Photo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Profile Profile { get; set; } = null!;
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "image/jpeg";
    public bool IsAvatar { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Interest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ICollection<UserInterest> UserInterests { get; set; } = new List<UserInterest>();
}

public sealed class UserInterest
{
    public Guid UserId { get; set; }
    public Guid InterestId { get; set; }
    public Profile Profile { get; set; } = null!;
    public Interest Interest { get; set; } = null!;
}
