namespace SkySaga.Game.Packets;

public class AnimationSequenceFadeComplete : ISerializablePacket
{


    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AnimationSequenceFadeComplete);

        return bitStream;
    }
}
