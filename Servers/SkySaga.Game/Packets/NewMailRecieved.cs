namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 143, server to client. Reader: FUN_007F83C0 -> FUN_00BB3E50(1, 0x8F, ...).
///
/// One optional UUID: a presence bit, then sixteen bytes as four 32-bit words if set.
/// </summary>
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
