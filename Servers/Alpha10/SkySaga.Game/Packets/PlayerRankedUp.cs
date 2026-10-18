namespace SkySaga.Game.Packets;

public class PlayerRankedUp : ISerializablePacket
{
    public int EntityId;
    public uint? JobId;
    public byte NewRank;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PlayerRankedUp);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, JobId);
        bitStream.WriteBits([NewRank], 8, true);

        return bitStream;
    }
}
