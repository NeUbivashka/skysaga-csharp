namespace SkySaga.Game.Packets;

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
