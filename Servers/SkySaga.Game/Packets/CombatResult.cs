namespace SkySaga.Game.Packets;

public class CombatResult : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CombatResult);

        return bitStream;
    }
}
