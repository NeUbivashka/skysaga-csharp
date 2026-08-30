namespace SkySaga.Game.Packets;

public class DebugRandomisePromotedShopItems : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRandomisePromotedShopItems);

        return bitStream;
    }
}
