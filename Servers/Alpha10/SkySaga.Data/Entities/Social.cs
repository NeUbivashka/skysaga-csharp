namespace SkySaga.Data.Entities;

public class CharacterFollow
{
    public Guid FollowerId { get; set; }
    public Guid FollowedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class FriendRequest
{
    public Guid RequesterId { get; set; }
    public Guid RecipientId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class Friendship
{
    public Guid CharacterId { get; set; }
    public Guid FriendId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CharacterBlock
{
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class BuildPermission
{
    public Guid OwnerId { get; set; }
    public Guid GranteeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
