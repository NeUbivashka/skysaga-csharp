namespace SkySaga.Game.Packets;

public class AlterActivePhaseByFactor : ISerializablePacket
{
    private const uint FactorMax = 0x20;

    private const float FactorScale = 0.03125f;

    public float Factor;

    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AlterActivePhaseByFactor);

        var units = (uint)Math.Clamp((int)(Factor / FactorScale), 0, (int)FactorMax);

        bitStream.WriteRanged(units, FactorMax);

        var idBytes = BitConverter.GetBytes(EntityId);
        Array.Reverse(idBytes);
        bitStream.WriteBits(idBytes, 32, true);

        return bitStream;
    }
}
