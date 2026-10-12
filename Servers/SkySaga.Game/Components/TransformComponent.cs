namespace SkySaga.Game.Components;

/// <summary>
/// Transform. Client bindings read function FUN_00BF88F0; binding indices Position 0, YawDegrees 1,
/// PitchDegrees 2, Scale 3 (FUN_00C9A270).
///
///   position     X ranged 0x10000, Y ranged 0x7F80 of (y + 0x3FC0), Z ranged 0x10000, all in
///                1/64 voxel - 17 + 15 + 17 bits (FUN_007D66A0, the same reader PlayerSpawned uses)
///   yawdegrees   ranged 0x6400 of (yaw + 0x3200), 1/32 degree, 15 bits (FUN_007D6510)
///   scale        ranged 0x38, 1/8, 6 bits (FUN_007D6410)
///
/// Position used to go out as three 17-bit fields with no Y offset: two bits too many and a wrong
/// Y, so every later parameter of the entity was read shifted (for the Player: the unlocked
/// feature set and the yaw). Scale wrote the raw bits of a float. No entity in this build has a
/// "size" binding, so Size is not synced.
/// </summary>
public class TransformComponent : Component
{
    public Vector3 Position { get; set => SetIfChanged(ref field, value); }
    public float YawDegrees { get; set => SetIfChanged(ref field, value); }
    public Vector3 Size { get; set => SetIfChanged(ref field, value); }
    public float Scale { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Position), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(ToFixed(Position.X, 0, 0x10000), 0x10000);
            bitStream.WriteRanged(ToFixed(Position.Y, 0x3FC0, 0x7F80), 0x7F80);
            bitStream.WriteRanged(ToFixed(Position.Z, 0, 0x10000), 0x10000);

            return true;
        }
        else if (parameterName.Equals(nameof(YawDegrees), StringComparison.OrdinalIgnoreCase))
        {
            var value = (int)MathF.Round(YawDegrees / 0.03125f) + 0x3200;

            bitStream.WriteRanged((uint)Math.Clamp(value, 0, 0x6400), 0x6400);

            return true;
        }
        else if (parameterName.Equals(nameof(Scale), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged((uint)Math.Clamp((int)MathF.Round(Scale * 8f), 0, 0x38), 0x38);

            return true;
        }

        return false;
    }

    /// <summary>A coordinate in 1/64 voxel, offset and clamped to the field's range.</summary>
    private static uint ToFixed(float value, int offset, int max)
        => (uint)Math.Clamp((int)MathF.Round(value * 64f) + offset, 0, max);
}
