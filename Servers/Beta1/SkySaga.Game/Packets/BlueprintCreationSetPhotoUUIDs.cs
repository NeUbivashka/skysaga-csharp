namespace SkySaga.Game.Packets;

public class BlueprintCreationSetPhotoUUIDs : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationSetPhotoUUIDs);

        return bitStream;
    }
}
