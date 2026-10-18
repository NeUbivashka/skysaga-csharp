namespace SkySaga.Game.Packets;

public class PartialChunkEditsSync : ISerializablePacket
{
    private const uint MaxAxisXZ = 32;
    private const uint MaxAxisY = 7;

    private const uint ChunkEditCountMax = 8;

    public Vector3Int ChunkCoord;

    public List<ChunkEdit> ChunkEditList = [];

    public bool Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PartialChunkEditsSync);

        bitStream.WriteRanged((uint)ChunkCoord.X, MaxAxisXZ);
        bitStream.WriteRanged((uint)ChunkCoord.Y, MaxAxisY);
        bitStream.WriteRanged((uint)ChunkCoord.Z, MaxAxisXZ);

        var count = (uint)ChunkEditList.Count;

        bitStream.WriteRanged(Math.Min(count, ChunkEditCountMax), ChunkEditCountMax);

        if (count >= ChunkEditCountMax)
        {
            bitStream.Write1();

            var countBytes = BitConverter.GetBytes(count);
            Array.Reverse(countBytes);
            bitStream.WriteBits(countBytes, 32, true);
        }

        foreach (var chunkEdit in ChunkEditList)
            chunkEdit.Serialize(bitStream);

        bitStream.Write(Unnamed);

        return bitStream;
    }
}
