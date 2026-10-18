namespace SkySaga.Game.Packets;

public class BuildingGridActivated : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BuildingGridActivated);

        return bitStream;
    }
}
