namespace SkySaga.Game.Packets;

public class ReturnInventoryItemJSON : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ReturnInventoryItemJSON);

        return bitStream;
    }
}
