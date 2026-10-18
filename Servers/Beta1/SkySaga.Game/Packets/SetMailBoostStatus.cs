namespace SkySaga.Game.Packets;

public class SetMailBoostStatus : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetMailBoostStatus);

        return bitStream;
    }
}
