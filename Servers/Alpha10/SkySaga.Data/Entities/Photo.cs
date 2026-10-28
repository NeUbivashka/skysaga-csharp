namespace SkySaga.Data.Entities;

public class Photo
{
    public Guid Id { get; set; }
    public Guid UploadToken { get; set; }
    public Guid OwnerCharacterId { get; set; }
    public Guid? WorldId { get; set; }
    public bool IsAvatar { get; set; }
    public bool CaptureOption { get; set; }
    public string? Caption { get; set; }
    public bool VisiblePublic { get; set; } = true;
    public bool VisibleFriends { get; set; } = true;
    public int Likes { get; set; }
    public float[] CameraPosition { get; set; } = [];
    public float[] PlayerPosition { get; set; } = [];
    public float[] FacingDirection { get; set; } = [];
    public float[] CameraDirection { get; set; } = [];
    public long[] EntityIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public string? Md5 { get; set; }
    public byte[]? Image { get; set; }
}
