namespace SkySaga.Game.Packets;

public class ReportPublishWorldError : ISerializablePacket
{
    public int ErrorCode;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ReportPublishWorldError);

        InventoryItemRef.WriteUInt32(bitStream, (uint)ErrorCode);

        return bitStream;
    }
}
