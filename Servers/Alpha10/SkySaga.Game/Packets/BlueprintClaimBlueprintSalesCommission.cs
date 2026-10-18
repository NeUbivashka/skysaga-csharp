namespace SkySaga.Game.Packets;

public class BlueprintClaimBlueprintSalesCommission : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintClaimBlueprintSalesCommission);

        return bitStream;
    }
}
