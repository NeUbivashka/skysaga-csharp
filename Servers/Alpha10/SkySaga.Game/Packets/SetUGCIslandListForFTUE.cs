namespace SkySaga.Game.Packets;

public class SetUGCIslandListForFTUE : ISerializablePacket
{
    private const uint IslandCountMax = 0x400;

    public List<Guid?> IslandUuids = [];

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetUGCIslandListForFTUE);

        var count = (uint)IslandUuids.Count;

        bitStream.WriteRanged(Math.Min(count, IslandCountMax), IslandCountMax);

        if (count >= IslandCountMax)
        {
            bitStream.Write1();

            var countBytes = BitConverter.GetBytes(count);
            Array.Reverse(countBytes);
            bitStream.WriteBits(countBytes, 32, true);
        }

        foreach (var uuid in IslandUuids)
            WriteUuid(bitStream, uuid);

        return bitStream;
    }

    private static void WriteUuid(BitStream bitStream, Guid? value)
    {
        bitStream.WriteClientUuid(value);
    }
}
