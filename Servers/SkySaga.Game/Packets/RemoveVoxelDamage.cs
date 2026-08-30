namespace SkySaga.Game.Packets;

public class RemoveVoxelDamage : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveVoxelDamage);

        return bitStream;
    }
}
