namespace SkySaga.Game.Packets;

public class BlueprintCreationSetTags : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationSetTags);

        return bitStream;
    }
}
