namespace SkySaga.Game.Packets;

public class BlueprintSourceDataRequested : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintSourceDataRequested);

        return bitStream;
    }
}
