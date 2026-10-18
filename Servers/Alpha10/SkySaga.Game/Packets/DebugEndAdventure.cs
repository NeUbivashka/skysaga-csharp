namespace SkySaga.Game.Packets;

public class DebugEndAdventure : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugEndAdventure);

        return bitStream;
    }
}
