namespace SkySaga.Game.Packets;

public class MysteryBoxOpenedNotification : ISerializablePacket
{
    public int EntityId;
    public int BoxId;
    public string? PlayerName;
    public MysteryBoxPrize Prize = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.MysteryBoxOpenedNotification);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)BoxId);

        bitStream.WriteString(PlayerName);

        Prize.Encode(bitStream);

        return bitStream;
    }
}
