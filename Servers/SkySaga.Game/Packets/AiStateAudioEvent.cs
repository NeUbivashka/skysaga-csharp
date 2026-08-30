namespace SkySaga.Game.Packets;

public class AiStateAudioEvent : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AiStateAudioEvent);

        return bitStream;
    }
}
