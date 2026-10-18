namespace SkySaga.Game.Packets;

public class DebugEquipmentChanged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugEquipmentChanged);

        return bitStream;
    }
}
