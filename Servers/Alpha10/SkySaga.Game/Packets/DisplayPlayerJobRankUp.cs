namespace SkySaga.Game.Packets;

public class DisplayPlayerJobRankUp : ISerializablePacket
{
    public Guid? PlayerUuid;
    public string? PlayerName;
    public bool Flag;
    public uint? JobId;
    public byte NewRank;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.DisplayPlayerJobRankUp);

        InventoryItemRef.WriteOptionalUuid(bitStream, PlayerUuid);
        bitStream.WriteString(PlayerName);
        bitStream.Write(Flag);
        InventoryItemRef.WriteOptionalUInt32(bitStream, JobId);
        bitStream.WriteBits([NewRank], 8, true);

        return bitStream;
    }
}
