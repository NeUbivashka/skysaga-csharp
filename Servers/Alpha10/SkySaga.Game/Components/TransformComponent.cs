namespace SkySaga.Game.Components;

public class TransformComponent : Component
{
    public Vector3 Position { get; set => SetIfChanged(ref field, value); }
    public float YawDegrees { get; set => SetIfChanged(ref field, value); }
    public float PitchDegrees { get; set => SetIfChanged(ref field, value); }
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
        else if (parameterName.Equals(nameof(PitchDegrees), StringComparison.OrdinalIgnoreCase))
        {
            var value = (int)MathF.Round(PitchDegrees / 0.03125f) + 0x3200;

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

    private static uint ToFixed(float value, int offset, int max)
        => (uint)Math.Clamp((int)MathF.Round(value * 64f) + offset, 0, max);
}
