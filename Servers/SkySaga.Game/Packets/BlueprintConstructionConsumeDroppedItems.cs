namespace SkySaga.Game.Packets;

public class BlueprintConstructionConsumeDroppedItems : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintConstructionConsumeDroppedItems);

        return bitStream;
    }
}
