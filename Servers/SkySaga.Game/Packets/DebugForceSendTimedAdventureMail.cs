namespace SkySaga.Game.Packets;

public class DebugForceSendTimedAdventureMail : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugForceSendTimedAdventureMail);

        return bitStream;
    }
}
