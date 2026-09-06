namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 49, server to client. Reader: FUN_00800780 -> FUN_00BB3E50(1, 0x31, ...).
///
/// A single 32-bit field, read at full width through the FUN_006FB780 path - so it is byte-swapped
/// on the way in and has to go out big-endian, the same rule as every other full-width field here.
/// The handler takes one argument and there is nothing else in the body.
///
/// The name is by role: the entity that stopped charging. The logger's name pointer does not survive
/// decompilation in this stub, but the width, the count and the byte order come from the reader.
/// </summary>
public class StopCharge : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StopCharge);

        var bytes = BitConverter.GetBytes(EntityId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
