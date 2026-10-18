namespace SkySaga.Game.Packets;

public class MapDefinition : ISerializablePacket
{
    public Vector3Int MapSizeChunks;

    public MapSpec Spec = new();

    public string AdventureType = string.Empty;

    public string MapFileName = string.Empty;

    public Guid? GroupLock;

    public uint? GameQueueCRC;

    public uint GameMode;

    private const uint GameModeMax = 0x10;

    public uint Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MapDefinition);

        Spec.Encode(bitStream);

        bitStream.WriteString(AdventureType);
        bitStream.WriteString(MapFileName);

        bitStream.WriteGuid(GroupLock);

        bitStream.WriteOptional(GameQueueCRC, (value) =>
        {
            WriteUInt32(bitStream, value);
        });

        bitStream.WriteRanged(GameMode, GameModeMax);

        WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
