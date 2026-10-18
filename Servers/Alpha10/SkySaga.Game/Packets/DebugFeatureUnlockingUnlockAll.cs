namespace SkySaga.Game.Packets;

public class DebugFeatureUnlockingUnlockAll : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugFeatureUnlockingUnlockAll);

        return bitStream;
    }
}
