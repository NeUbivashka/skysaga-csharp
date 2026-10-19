namespace SkySaga.Game.Packets;

public class AlterActivePhaseByFactor : ISerializablePacket
{
    private const uint FactorMax = 0x20;

    public float Factor;

    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AlterActivePhaseByFactor);

        bitStream.WriteRanged((uint)Math.Clamp((int)MathF.Round(Factor * FactorMax), 0, (int)FactorMax), FactorMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
