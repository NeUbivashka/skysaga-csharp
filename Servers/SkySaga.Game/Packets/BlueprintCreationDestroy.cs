namespace SkySaga.Game.Packets;

public class BlueprintCreationDestroy : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationDestroy);

        return bitStream;
    }
}
