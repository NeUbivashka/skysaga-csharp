namespace SkySaga.Game.Packets;

public class BlueprintConstructionComplete : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintConstructionComplete);

        return bitStream;
    }
}
