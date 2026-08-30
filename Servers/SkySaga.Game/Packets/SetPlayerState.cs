namespace SkySaga.Game.Packets;

public class SetPlayerState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetPlayerState);

        return bitStream;
    }
}
