namespace SkySaga.Game.Packets;

public class BlueprintConstructionCompleteWithHardCurrency : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintConstructionCompleteWithHardCurrency);

        return bitStream;
    }
}
