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
            bitStream.WriteBits(BitConverter.GetBytes(value), 32, true);
        });

        bitStream.WriteRanged(GameMode, GameModeMax);

        bitStream.WriteBits(BitConverter.GetBytes(Unnamed), 32, true);

        return bitStream;
    }
}
