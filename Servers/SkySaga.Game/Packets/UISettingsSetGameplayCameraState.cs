namespace SkySaga.Game.Packets;

public class UISettingsSetGameplayCameraState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSetGameplayCameraState);

        return bitStream;
    }
}
