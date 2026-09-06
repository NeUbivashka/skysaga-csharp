namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 324, server to client. Reader: FUN_007F6630 -> FUN_00BB3E50(1, 0x144, ...).
///
///     IsOpen         1 bit
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class InstanceOpenForMatchmakingNewPlayersState : ISerializablePacket
{
    public bool IsOpen;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.InstanceOpenForMatchmakingNewPlayersState);

        bitStream.Write(IsOpen);

        return bitStream;
    }
}
