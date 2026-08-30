namespace SkySaga.Game.Packets;

public class PlacedVoxel : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlacedVoxel);

        return bitStream;
    }
}
