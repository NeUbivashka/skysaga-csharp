namespace SkySaga.Game.Packets;

public class UpdateVoxelDamage : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UpdateVoxelDamage);

        return bitStream;
    }
}
