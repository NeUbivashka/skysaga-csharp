namespace SkySaga.Game.Packets;

public class BlueprintRequestSourceData : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintRequestSourceData);

        return bitStream;
    }
}
