namespace SkySaga.Game.Packets;

public class BlueprintShopRevoke : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintShopRevoke);

        return bitStream;
    }
}
