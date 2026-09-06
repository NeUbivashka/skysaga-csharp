namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 255, server to client. Reader: FUN_007F42D0 -> FUN_00BB3E50(1, 0xFF, ...).
///
///     Immediate      1 bit
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class ForcePhotoCapture : ISerializablePacket
{

    public bool Immediate;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ForcePhotoCapture);

        bitStream.Write(Immediate);

        return bitStream;
    }
}
