namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 100, server to client. Reader: FUN_007F0880 -> FUN_00BB3E50(1, 0x64, ...).
///
///     RaceUuid   1 presence bit + 4 x 32 bits
///     Result     32 bits, full width -> big-endian
/// </summary>
public class EndRace : ISerializablePacket
{
    public Guid? RaceUuid;
    public int Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EndRace);

        InventoryItemRef.WriteOptionalUuid(bitStream, RaceUuid);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Result);

        return bitStream;
    }
}
