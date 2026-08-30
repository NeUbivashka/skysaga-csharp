namespace SkySaga.Game.Packets.Common;

public class ChunkEdit : ISerializableType
{
    public byte VoxelIndex;

    public List<Vector3Int> VoxelCoordList = [];
    private const int VoxelCoordListDefaultCount = 7;

    public void Serialize(BitStream bitStream)
    {
        bitStream.WriteBits([VoxelIndex], 8, true);

        if (VoxelCoordList.Count < VoxelCoordListDefaultCount)
        {
            bitStream.WriteBits(BitConverter.GetBytes(VoxelCoordList.Count - 1), 32 - Util.NumBitsRequiredUInt32(VoxelCoordListDefaultCount), true);
        }
        else
        {
            bitStream.WriteBits(BitConverter.GetBytes(VoxelCoordListDefaultCount - 1), 32 - Util.NumBitsRequiredUInt32(VoxelCoordListDefaultCount), true);

            bitStream.Write1();
            bitStream.Write(VoxelCoordList.Count);
        }

        foreach (var voxelCoord in VoxelCoordList)
        {
            bitStream.WriteBits(BitConverter.GetBytes(voxelCoord.X), 32 - Util.NumBitsRequiredUInt32(32), true);
            bitStream.WriteBits(BitConverter.GetBytes(voxelCoord.Y), 32 - Util.NumBitsRequiredUInt32(32), true);
            bitStream.WriteBits(BitConverter.GetBytes(voxelCoord.Z), 32 - Util.NumBitsRequiredUInt32(32), true);
        }
    }
}