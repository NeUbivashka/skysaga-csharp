namespace SkySaga.Game.Packets;

public class AiStateAudioEvent : ISerializablePacket
{
    private const uint AudioStateMax = 0xD;
    public int EntityId;
    public uint AudioState;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.AiStateAudioEvent);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(AudioState, AudioStateMax), AudioStateMax);

        return bitStream;
    }
}
