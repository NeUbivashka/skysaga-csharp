namespace SkySaga.Game.Packets;

public class PlayerHitByAnother : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerHitByAnother);

        return bitStream;
    }
}
