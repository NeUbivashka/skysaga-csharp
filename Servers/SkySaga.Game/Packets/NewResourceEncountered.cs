namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 58, server to client. Reader: FUN_007F8710 -> FUN_00BB3E50(1, 0x3A, ...).
///
/// The entire body is one <see cref="InventoryItemRef"/> (FUN_007E9440). Nothing precedes or
/// follows it.
/// </summary>
public class NewResourceEncountered : ISerializablePacket
{
    public InventoryItemRef Item = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NewResourceEncountered);

        Item.Encode(bitStream);

        return bitStream;
    }
}
