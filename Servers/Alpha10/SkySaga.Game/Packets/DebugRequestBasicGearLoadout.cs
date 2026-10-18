namespace SkySaga.Game.Packets;

public class DebugRequestBasicGearLoadout : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestBasicGearLoadout);

        return bitStream;
    }
}
