namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 41, server to client. Reader: FUN_007F9A50 -> FUN_00BB3E50(1, 0x29, ...).
///
/// One string, read with FUN_007EA530, and nothing else. The handler takes a single argument.
///
/// This is the chat/roster notification, not the entity spawn - EntityAdd already carries the
/// arrival of the body. Send it alongside the broadcast in ClientReadyToPlay when a second player
/// is actually being tested; with one client it has nowhere to go.
/// </summary>
public class PlayerJoined : ISerializablePacket
{
    public string? PlayerName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerJoined);

        bitStream.WriteString(PlayerName);

        return bitStream;
    }
}
