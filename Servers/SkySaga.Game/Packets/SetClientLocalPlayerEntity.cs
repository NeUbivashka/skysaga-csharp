namespace SkySaga.Game.Packets;

public class SetClientLocalPlayerEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetClientLocalPlayerEntity);

        return bitStream;
    }
}
