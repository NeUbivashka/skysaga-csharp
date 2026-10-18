namespace SkySaga.Game.Packets;

public class DebugAddPlayerPremiumSeconds : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAddPlayerPremiumSeconds);

        return bitStream;
    }
}
