namespace SkySaga.Game.Packets;

public class NewMailRecieved : ISerializablePacket
{
    public Guid? MailUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.NewMailRecieved);

        InventoryItemRef.WriteOptionalUuid(bitStream, MailUuid);

        return bitStream;
    }
}
