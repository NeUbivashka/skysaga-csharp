namespace SkySaga.Game.Packets;

/// <summary>
/// Alpha 10 layout, read out of the client.
///
/// Stub:     FUN_007FAC30  ->  FUN_00BB3E50(1, 0x9F, ...)
/// Position: FUN_007D66A0
/// Angles:   FUN_007D6510
///
/// The packet used to go out empty - id only, no body. The client reads four fields from it:
///
///     EntityId    32 bits
///     Position    X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Yaw         15 bits, biased by -0x3200, clamped to +-0x3200
///     Pitch       15 bits, same encoding as Yaw
///
/// With an empty body those reads fall through and the client keeps the defaults it primed the
/// locals with: position (0, -0x3FC0, 0) and both angles at -0x3200. So the player was being
/// spawned at a default point facing a default direction, regardless of anything the server
/// thought it had placed.
///
/// Note the Y component. Entity sync (TransformComponent.TrySync) writes all three position
/// components as 17 bits with no bias; this packet does not. Y here is 15 bits over 0..0x7F80
/// with -0x3FC0 applied on read, which is +-16320 units, or +-255 world units at the 64
/// units-per-world-unit scale the rest of the project uses. Do not copy the sync encoding here.
///
/// Field names could not be recovered - the logger's name pointers do not survive decompilation
/// in this stub - so these are named by role. The order and the widths come from the reader and
/// are solid.
/// </summary>
public class PlayerSpawned : ISerializablePacket
{
    /// <summary>Units per world unit, same scale the entity sync uses.</summary>
    private const float PositionScale = 64f;

    /// <summary>Degrees per angle unit, same scale <c>TransformComponent.YawDegrees</c> uses.</summary>
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
