namespace SkySaga.Game.Packets;

public class GuildNotification : ISerializablePacket
{
    private const uint KindMax = 6;

    public uint Kind;
    public Guid? GuildUuid;
    public string? PlayerName;
    public string? Message;
    public Guid? TargetUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GuildNotification);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GuildUuid);
        bitStream.WriteString(PlayerName);
        bitStream.WriteString(Message);
        InventoryItemRef.WriteOptionalUuid(bitStream, TargetUuid);

        return bitStream;
    }
}
