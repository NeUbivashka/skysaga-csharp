using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;

namespace SkySaga.Game.Database;

public static class PhotoStore
{
    public static Photo Create(Guid ownerCharacterId, Guid worldId, bool isAvatar, bool captureOption,
        float[] cameraPosition, float[] playerPosition, float[] facingDirection, float[] cameraDirection, long[] entityIds)
    {
        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            UploadToken = Guid.NewGuid(),
            OwnerCharacterId = ownerCharacterId,
            WorldId = worldId,
            IsAvatar = isAvatar,
            CaptureOption = captureOption,
            CameraPosition = cameraPosition,
            PlayerPosition = playerPosition,
            FacingDirection = facingDirection,
            CameraDirection = cameraDirection,
            EntityIds = entityIds,
            CreatedAt = DateTimeOffset.UtcNow
        };

        using var context = GameDatabase.Context();
        context.Photos.Add(photo);
        context.SaveChanges();

        return photo;
    }

    public static bool Delete(Guid photoId, Guid ownerCharacterId)
    {
        using var context = GameDatabase.Context();
        return context.Photos.Where(p => p.Id == photoId && p.OwnerCharacterId == ownerCharacterId).ExecuteDelete() > 0;
    }
}
