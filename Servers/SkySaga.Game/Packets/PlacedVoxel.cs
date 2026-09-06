namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 101, server to client. Reader: FUN_007F92A0 -> FUN_00BB3E50(1, 0x65, ...).
///
///     EntityId    32 bits, full width -> big-endian
///     VoxelType   8 bits, read raw with no swap
///
/// The second read is a bare FUN_00EA6E40 with a width of 8 and no FUN_006FB780 guard around it,
/// so it is a plain byte rather than a ranged field.
/// </summary>
public class PlacedVoxel : ISerializablePacket
{
    public int EntityId;
    public byte VoxelType;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PlacedVoxel);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteBits([VoxelType], 8, true);

        return bitStream;
    }
}
