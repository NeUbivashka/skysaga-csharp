namespace SkySaga.Game.Packets;

public class EquipmentMaterialChanged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EquipmentMaterialChanged);

        return bitStream;
    }
}
