namespace SkySaga.Game.Packets;

public class InputStateChanged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InputStateChanged);

        return bitStream;
    }
}
