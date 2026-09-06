namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 56, server to client. Reader: FUN_007EF4C0 -> FUN_00BB3E50(1, 0x38, ...).
///
/// A single 32-bit field, read at full width through the FUN_006FB780 path - so it is byte-swapped
/// on the way in and has to go out big-endian, the same rule as every other full-width field here.
/// The handler takes one argument and there is nothing else in the body.
///
/// The name is by role: the entity whose craft failed. The logger's name pointer does not survive
/// decompilation in this stub, but the width, the count and the byte order come from the reader.
/// </summary>
public class CraftingFailed : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftingFailed);

        var bytes = BitConverter.GetBytes(EntityId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
