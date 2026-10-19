namespace SkySaga.Game.Components;

public class ClientResourcePickupComponent : Component
{
    private const uint ThrowTypeMax = 7;
    private const uint VelocityMax = 0xFFC0;
    private const int VelocityOffset = 0x7FE0;
    private const float VelocityScale = 0.03125f;

    public int InventoryItemEntity { get; set => SetIfChanged(ref field, value); }
    public int OwningEntity { get; set => SetIfChanged(ref field, value); }
    public int CreatingEntity { get; set => SetIfChanged(ref field, value); }
    public int SourceEntity { get; set => SetIfChanged(ref field, value); }
    public byte ThrowType { get; set => SetIfChanged(ref field, value); }
    public Vector3 StartPosition { get; set => SetIfChanged(ref field, value); }
    public Vector3 StartVelocity { get; set => SetIfChanged(ref field, value); }
    public Vector3 TargetPosition { get; set => SetIfChanged(ref field, value); }
    public Vector3 TargetVelocity { get; set => SetIfChanged(ref field, value); }
    public bool PickupEnabled { get; set => SetIfChanged(ref field, value); }
    public bool EditMapRequiredToPickup { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        switch (parameterName.ToLowerInvariant())
        {
            case "inventoryitementity":
                return WriteEntity(bitStream, InventoryItemEntity);
            case "owningentity":
                bitStream.WriteFullWidthUInt32((uint)OwningEntity);
                return true;
            case "creatingentity":
                return WriteEntity(bitStream, CreatingEntity);
            case "sourceentity":
                return WriteEntity(bitStream, SourceEntity);
            case "throwtype":
                bitStream.WriteRanged(Math.Min(ThrowType, ThrowTypeMax), ThrowTypeMax);
                return true;
            case "startposition":
                WritePosition(bitStream, StartPosition);
                return true;
            case "startvelocity":
                WriteVelocity(bitStream, StartVelocity);
                return true;
            case "targetposition":
                WritePosition(bitStream, TargetPosition);
                return true;
            case "targetvelocity":
                WriteVelocity(bitStream, TargetVelocity);
                return true;
            case "pickupenabled":
                bitStream.Write(PickupEnabled);
                return true;
            case "editmaprequiredtopickup":
                bitStream.Write(EditMapRequiredToPickup);
                return true;
            default:
                return false;
        }
    }

    private static bool WriteEntity(BitStream bitStream, int entityId)
    {
        if (entityId == 0)
            return false;

        bitStream.WriteFullWidthUInt32((uint)entityId);

        return true;
    }

    internal static void WritePosition(BitStream bitStream, Vector3 position)
    {
        bitStream.WriteRanged(ToFixed(position.X * 64f, 0, 0x10000), 0x10000);
        bitStream.WriteRanged(ToFixed(position.Y * 64f, 0x3FC0, 0x7F80), 0x7F80);
        bitStream.WriteRanged(ToFixed(position.Z * 64f, 0, 0x10000), 0x10000);
    }

    private static void WriteVelocity(BitStream bitStream, Vector3 velocity)
    {
        bitStream.WriteRanged(ToFixed(velocity.X / VelocityScale, VelocityOffset, VelocityMax), VelocityMax);
        bitStream.WriteRanged(ToFixed(velocity.Y / VelocityScale, VelocityOffset, VelocityMax), VelocityMax);
        bitStream.WriteRanged(ToFixed(velocity.Z / VelocityScale, VelocityOffset, VelocityMax), VelocityMax);
    }

    private static uint ToFixed(float scaled, int offset, uint max)
        => (uint)Math.Clamp((int)MathF.Round(scaled) + offset, 0, (int)max);
}
