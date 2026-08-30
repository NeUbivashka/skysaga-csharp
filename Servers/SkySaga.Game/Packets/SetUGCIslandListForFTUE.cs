namespace SkySaga.Game.Packets;

public class SetUGCIslandListForFTUE : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetUGCIslandListForFTUE);

        return bitStream;
    }
}
