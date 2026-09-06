namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 33, server to client. Reader: FUN_007FCA30 -> FUN_00BB3E50(1, 0x21, ...).
///
/// A single 32-bit field, read at full width through the FUN_006FB780 path, so it goes out
/// big-endian. Nothing else in the body.
///
/// Name by role - the counterpart to UpdateVoxelDamage (32), which carries a position and several
/// more fields. Clearing damage only needs to say which one.
/// </summary>
public class RemoveVoxelDamage : ISerializablePacket
{
    public int DamageId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveVoxelDamage);

        var bytes = BitConverter.GetBytes(DamageId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
