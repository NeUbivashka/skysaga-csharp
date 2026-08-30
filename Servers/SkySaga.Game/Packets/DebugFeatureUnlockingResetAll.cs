namespace SkySaga.Game.Packets;

public class DebugFeatureUnlockingResetAll : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugFeatureUnlockingResetAll);

        return bitStream;
    }
}
