namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 168, server to client. Reader: FUN_007F7630 -> FUN_00BB3E50(1, 0xA8, ...).
///
///     EntityId     32 bits, full width -> big-endian
///     BoxId        32 bits, full width -> big-endian
///     PlayerName   string
///     Prize        one MysteryBoxPrize (FUN_007E92D0)
/// </summary>
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
