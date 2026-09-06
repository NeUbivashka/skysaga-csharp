namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 42, server to client. Reader: FUN_007FA4F0 -> FUN_00BB3E50(1, 0x2A, ...).
///
/// One string, same shape as PlayerJoined. The counterpart to the EntityRemoved that
/// PlayerConnection.OnDisconnected already broadcasts.
/// </summary>
public class PlayerLeft : ISerializablePacket
{
    public string? PlayerName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerLeft);

        bitStream.WriteString(PlayerName);

        return bitStream;
    }
}
