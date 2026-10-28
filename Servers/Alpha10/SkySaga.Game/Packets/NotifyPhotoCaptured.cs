using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class NotifyPhotoCaptured
{
    private const uint CaptureIdShortMax = 0x7F;
    private const uint CoordinateMax = 0x10000;
    private const uint HeightMax = 0x7F80;
    private const int HeightOffset = 0x3FC0;
    private const uint DirectionMax = 0x80;
    private const int DirectionOffset = 0x40;
    private const float FixedPointScale = 64f;
    private const uint EntityListEscape = 0x80;
    private const uint EntityListLimit = 0x400;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!ReadCaptureId(bitStream, out var captureId)
            || !ReadPosition(bitStream, out var cameraPosition)
            || !ReadPosition(bitStream, out var playerPosition)
            || !ReadDirection(bitStream, out var facingDirection)
            || !ReadDirection(bitStream, out var cameraDirection)
            || !bitStream.ReadInt32(1, out var avatar)
            || !bitStream.ReadInt32(1, out var captureOption)
            || !bitStream.ReadEscapedCount(EntityListEscape, out var count)
            || count > EntityListLimit)
            return false;

        var entityIds = new long[count];

        for (var i = 0; i < count; i++)
        {
            if (!bitStream.ReadFullWidthUInt32(out var entityId))
                return false;

            entityIds[i] = entityId;
        }

        if (!connection.IsPlayerInitialized || connection.CharacterId is not { } characterId)
        {
            Console.WriteLine($"[Photo] capture {captureId} from {connection.Guid} ignored: no character");
            return true;
        }

        var photo = PhotoStore.Create(characterId, connection.WorldManager.Info.Id, avatar != 0, captureOption != 0,
            cameraPosition, playerPosition, facingDirection, cameraDirection, entityIds);

        connection.Send(new PhotoValidated { CaptureId = captureId, PhotoUuid = photo.Id, UploadToken = photo.UploadToken });

        Console.WriteLine($"[Photo] {connection.CharacterName} captured {(avatar != 0 ? "avatar " : "")}photo {photo.Id} (capture {captureId}) "
            + $"at {string.Join(", ", playerPosition)}, {count} entities in view");

        return true;
    }

    private static bool ReadCaptureId(BitStream bitStream, out uint captureId)
    {
        captureId = 0;

        if (!bitStream.ReadInt32(1, out var wide))
            return false;

        if (wide != 0)
            return bitStream.ReadFullWidthUInt32(out captureId);

        if (!bitStream.ReadInt32(CaptureIdShortMax, out var value))
            return false;

        captureId = (uint)value;
        return true;
    }

    private static bool ReadPosition(BitStream bitStream, out float[] position)
    {
        position = [];

        if (!bitStream.ReadInt32(CoordinateMax, out var x)
            || !bitStream.ReadInt32(HeightMax, out var y)
            || !bitStream.ReadInt32(CoordinateMax, out var z))
            return false;

        position = [x / FixedPointScale, (y - HeightOffset) / FixedPointScale, z / FixedPointScale];
        return true;
    }

    private static bool ReadDirection(BitStream bitStream, out float[] direction)
    {
        direction = [];

        if (!bitStream.ReadInt32(DirectionMax, out var x)
            || !bitStream.ReadInt32(DirectionMax, out var y)
            || !bitStream.ReadInt32(DirectionMax, out var z))
            return false;

        direction = [(x - DirectionOffset) / FixedPointScale, (y - DirectionOffset) / FixedPointScale, (z - DirectionOffset) / FixedPointScale];
        return true;
    }
}
