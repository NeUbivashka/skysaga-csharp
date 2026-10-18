namespace SkySaga.Game.Packets;

public class PlayerKickConfirmed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerKickConfirmed);

        return bitStream;
    }
}
