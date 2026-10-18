namespace SkySaga.Game.Packets;

public class DebugFeatureUnlockingSetUpdatesDisabled : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugFeatureUnlockingSetUpdatesDisabled);

        return bitStream;
    }
}
