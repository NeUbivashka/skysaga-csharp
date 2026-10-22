namespace SkySaga.Game.Packets;

public class NotifyPhotoCaptured : ISerializablePacket
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.Read(out byte captureId))
            return false;

        var guidBytes = new byte[16];
        if (!bitStream.Read(guidBytes, 16u))
            return false;

        var photoHex = Convert.ToHexString(guidBytes).ToLowerInvariant();
        if (!Guid.TryParse(photoHex, out var photoUuid))
            return false;

        var sizeBytes = new byte[4];
        uint fileSize = 0;
        if (bitStream.GetNumberOfUnreadBits() >= 32 && bitStream.Read(sizeBytes, 4u))
        {
            fileSize = BitConverter.ToUInt32(sizeBytes, 0);
        }

        Console.WriteLine($"[Photo] NotifyPhotoCaptured: captureId={captureId}, photoUuid={photoUuid:D}, size={fileSize} from {connection.Identity.Name} ({connection.Identity.CharacterUuid})");

        var response = new PhotoValidated
        {
            Result = captureId,
            PhotoUuid = photoUuid,
            OwnerUuid = connection.Identity.CharacterUuid
        };

        connection.Send(response);
        Console.WriteLine($"[Photo] Sent PhotoValidated to {connection.Identity.Name}: Result={captureId}, PhotoUuid={photoUuid:D}");

        return true;
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NotifyPhotoCaptured);

        return bitStream;
    }
}
