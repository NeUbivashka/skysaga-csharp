namespace SkySaga.Game.Packets;

public class BlueprintSaleClaimResult : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintSaleClaimResult);

        return bitStream;
    }
}
