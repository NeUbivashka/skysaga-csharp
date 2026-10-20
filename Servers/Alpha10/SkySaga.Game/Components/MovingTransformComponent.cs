namespace SkySaga.Game.Components;

public class MovingTransformComponent : TransformComponent
{
    private const uint VelocityMax = 0xFFC0;
    private const int VelocityOffset = 0x7FE0;
    private const float VelocityScale = 0.03125f;

    public Vector3 Velocity { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Velocity), StringComparison.OrdinalIgnoreCase))
        {
            WriteComponent(bitStream, Velocity.X);
            WriteComponent(bitStream, Velocity.Y);
            WriteComponent(bitStream, Velocity.Z);
            return true;
        }

        return base.TrySync(parameterName, bitStream);
    }

    private static void WriteComponent(BitStream bitStream, float value)
        => bitStream.WriteRanged((uint)Math.Clamp((int)MathF.Round(value / VelocityScale) + VelocityOffset, 0, (int)VelocityMax), VelocityMax);
}
