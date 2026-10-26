namespace SkySaga.Data.Entities;

public enum AccountRole
{
    Player,
    GameMaster,
    Admin
}

public class Account
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public string? PasswordHash { get; set; }
    public AccountRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? BannedUntil { get; set; }
    public string? BanReason { get; set; }

    public List<Character> Characters { get; set; } = [];
}
