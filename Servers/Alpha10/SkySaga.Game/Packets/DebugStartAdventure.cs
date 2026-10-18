namespace SkySaga.Game.Packets;

public class DebugStartAdventure : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugStartAdventure);

        return bitStream;
    }
}
