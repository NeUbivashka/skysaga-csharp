namespace SkySaga.Game.Packets.Common;

public class ChunkEdit : ISerializableType
{
    private const uint VoxelCoordCountEscape = 8;
    private const uint LocalCoordMax = 0x20;

    public byte VoxelIndex;

    public List<Vector3Int> VoxelCoordList = [];

    public void Serialize(BitStream bitStream)
    {
        bitStream.WriteBits([VoxelIndex], 8, true);
        bitStream.WriteEscapedCount((uint)VoxelCoordList.Count, VoxelCoordCountEscape);

        foreach (var voxelCoord in VoxelCoordList)
        {
            bitStream.WriteRanged((uint)voxelCoord.X, LocalCoordMax);
            bitStream.WriteRanged((uint)voxelCoord.Y, LocalCoordMax);
            bitStream.WriteRanged((uint)voxelCoord.Z, LocalCoordMax);
        }
    }
}
