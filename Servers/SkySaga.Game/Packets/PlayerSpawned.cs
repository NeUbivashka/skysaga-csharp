namespace SkySaga.Game.Packets;

public class PlayerSpawned : ISerializablePacket
{
    private const float PositionScale = 64f;

    private const float AngleScale = 0.03125f;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;

    private const uint AngleMax = 0x6400;
    private const int AngleBias = 0x3200;

    public int EntityId;

    public Vector3 Position;

    public float YawDegrees;

    public float PitchDegrees;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerSpawned);

        bitStream.Write(EntityId);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        WriteAngle(bitStream, YawDegrees);
        WriteAngle(bitStream, PitchDegrees);

        return bitStream;
    }

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        var units = (int)(value * PositionScale);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax),
            true);
    }

    private static void WriteVertical(BitStream bitStream, float value)
    {
        var units = (int)(value * PositionScale) + PositionVerticalBias;

        units = Math.Clamp(units, 0, (int)PositionVerticalMax);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax),
            true);
    }

    private static void WriteAngle(BitStream bitStream, float degrees)
    {
        var units = (int)(degrees / AngleScale) + AngleBias;

        units = Math.Clamp(units, 0, (int)AngleMax);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(AngleMax),
            true);
    }
}
