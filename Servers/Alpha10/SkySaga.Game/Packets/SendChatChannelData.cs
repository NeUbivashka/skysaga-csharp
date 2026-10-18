using System.IO;
using System.Text;

namespace SkySaga.Game.Packets;

public class SendChatChannelData : ISerializablePacket
{
    private const byte Ordinal = 243;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.WriteBits(BitConverter.GetBytes(8), 32 - Util.NumBitsRequiredUInt32(8));
        bitStream.Write1();
        bitStream.Write(1);

        bitStream.WriteBits(BitConverter.GetBytes(1), 32 - Util.NumBitsRequiredUInt32(8));
        bitStream.WriteString("WorldChat");
        bitStream.Write1();

        bitStream.WriteString("System");
        bitStream.WriteString("Global");

        return bitStream;
    }
}
