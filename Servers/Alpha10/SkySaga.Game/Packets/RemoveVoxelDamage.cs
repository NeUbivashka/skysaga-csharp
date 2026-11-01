namespace SkySaga.Game.Packets;

public class RemoveVoxelDamage : ISerializablePacket
{
    public uint VoxelId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveVoxelDamage);

        var bytes = BitConverter.GetBytes(VoxelId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
