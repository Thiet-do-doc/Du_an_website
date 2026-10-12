namespace DatingApp.Api.Demo;

// Demo-only stand-ins for TV1's User, TV3's Match and TV5's Block entities.
public class DemoUser
{
    public Guid Id { get; set; }
    public string Alias { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; } = true;
}

public class DemoMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAId { get; set; }
    public Guid UserBId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DemoBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
}

public static class DemoIds
{
    public static readonly Guid A = Guid.Parse("00000000-0000-4000-a000-00000000000a");
    public static readonly Guid B = Guid.Parse("00000000-0000-4000-a000-00000000000b");
    public static readonly Guid C = Guid.Parse("00000000-0000-4000-a000-00000000000c");
    public static readonly Guid AB = Guid.Parse("11111111-1111-4111-a111-111111111111");
}
