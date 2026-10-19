namespace SkySaga.Game.Packets;

public class XPGained : ISerializablePacket
{
    public int EntityId;
    public uint Experience;
    public int? JobChallengeIndex;
    public int JobIndex;
    public uint? TargetCrc;
    public uint BonusExperience;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.XPGained);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, Experience);
        bitStream.WriteRangedIndex(JobChallengeIndex is int challenge ? (uint)challenge + 1 : 0, (uint)JobChallengeTable.Count + 1);
        bitStream.WriteRangedIndex((uint)JobIndex + 1, (uint)JobTable.Count + 1);

        if (TargetCrc is uint crc)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, crc);
        }
        else
            bitStream.Write0();

        bitStream.Write0();
        bitStream.Write0();
        bitStream.Write0();
        InventoryItemRef.WriteUInt32(bitStream, BonusExperience);

        return bitStream;
    }
}
