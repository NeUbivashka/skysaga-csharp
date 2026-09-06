namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 234, server to client. Reader: FUN_007FD110 -> FUN_00BB3E50(1, 0xEA, ...).
///
///     ActionListId   1 presence bit + 32 bits
///     EntityId       32 bits, full width -> big-endian
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class RunMeandrosActions : ISerializablePacket
{

    public uint? ActionListId;
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RunMeandrosActions);

        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionListId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
