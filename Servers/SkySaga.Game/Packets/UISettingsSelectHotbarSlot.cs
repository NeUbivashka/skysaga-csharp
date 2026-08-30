namespace SkySaga.Game.Packets;

public class UISettingsSelectHotbarSlot : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSelectHotbarSlot);

        return bitStream;
    }
}
