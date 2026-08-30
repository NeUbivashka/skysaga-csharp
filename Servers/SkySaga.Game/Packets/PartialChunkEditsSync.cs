namespace SkySaga.Game.Packets;

public class PartialChunkEditsSync : ISerializablePacket
{
    public Vector3Int ChunkCoord;

    public List<ChunkEdit> ChunkEditList = [];
    private const int _chunkEditListDefaultCount = 7;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PartialChunkEditsSync);

        bitStream.WriteBits(BitConverter.GetBytes(ChunkCoord.X), 32 - Util.NumBitsRequiredUInt32(32), true);
        bitStream.WriteBits(BitConverter.GetBytes(ChunkCoord.Y), 32 - Util.NumBitsRequiredUInt32(32), true);
        bitStream.WriteBits(BitConverter.GetBytes(ChunkCoord.Z), 32 - Util.NumBitsRequiredUInt32(32), true);

        if (ChunkEditList.Count < _chunkEditListDefaultCount)
        {
            bitStream.WriteBits(BitConverter.GetBytes(ChunkEditList.Count - 1), 32 - Util.NumBitsRequiredUInt32(_chunkEditListDefaultCount), true);
        }
        else
        {
            bitStream.WriteBits(BitConverter.GetBytes(_chunkEditListDefaultCount - 1), 32 - Util.NumBitsRequiredUInt32(_chunkEditListDefaultCount), true);

            bitStream.Write1();
            bitStream.Write(ChunkEditList.Count);
        }

        foreach (var chunkEdit in ChunkEditList)
            chunkEdit.Serialize(bitStream);

        return bitStream;
    }
}