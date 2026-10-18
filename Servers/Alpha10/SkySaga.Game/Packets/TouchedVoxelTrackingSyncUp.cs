namespace SkySaga.Game.Packets;

public class TouchedVoxelTrackingSyncUp : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TouchedVoxelTrackingSyncUp);

        return bitStream;
    }
}
