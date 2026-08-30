namespace SkySaga.Game.Packets;

public class ChunkSync : ISerializablePacket
{
    private const uint MaxAxisXZ = 32;
    private const uint MaxAxisY = 7;

    public Vector3Int Coords;

    public byte[]? Data1;
    public byte[]? Data2;

    public byte? AdjacentChunks;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ChunkSync);

        bitStream.WriteRanged((uint)Coords.X, MaxAxisXZ);
        bitStream.WriteRanged((uint)Coords.Y, MaxAxisY);
        bitStream.WriteRanged((uint)Coords.Z, MaxAxisXZ);

        bitStream.WriteOptional(Data1, (value) =>
        {





            var lengthBytes = BitConverter.GetBytes(value.Length);
            Array.Reverse(lengthBytes);
            bitStream.WriteBits(lengthBytes, 32, true);
            bitStream.WriteBits(value, (uint)value.Length * 8, true);
        });

        bitStream.WriteOptional(Data2, (value) =>
        {

            var lengthBytes = BitConverter.GetBytes(value.Length);
            Array.Reverse(lengthBytes);
            bitStream.WriteBits(lengthBytes, 32, true);
            bitStream.WriteBits(value, (uint)value.Length * 8, true);
        });

        bitStream.WriteOptional(AdjacentChunks, (value) =>
        {
            bitStream.WriteBits([value], 8, true);
        });

        return bitStream;
    }
}
