namespace SkySaga.Game.Components;

/// <summary>
/// Character physics. On top of the physics flags it carries the movement input
/// (inputmovementdirection, inputmovementspeed), which the client reports with RPCSetInputMovement
/// and expects back through sync. Encodings as in the client's RPC writers:
///
///   inputmovementdirection   3 x ranged max 0x80 of (component + 0x40), i.e. -64..63 (FUN_008621B0)
///   inputmovementspeed       ranged max 0x78 (FUN_00B23870)
/// </summary>
public class ClientCharacterPhysicsComponent : ClientPhysicsComponent
{
    public const uint DirectionMax = 0x80;
    public const uint SpeedMax = 0x78;

    public (int X, int Y, int Z) InputMovementDirection { get; set => SetIfChanged(ref field, value); }
    public uint InputMovementSpeed { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(InputMovementDirection), StringComparison.OrdinalIgnoreCase))
        {
            var (x, y, z) = InputMovementDirection;

            bitStream.WriteRanged((uint)Math.Clamp(x + 0x40, 0, 0x80), DirectionMax);
            bitStream.WriteRanged((uint)Math.Clamp(y + 0x40, 0, 0x80), DirectionMax);
            bitStream.WriteRanged((uint)Math.Clamp(z + 0x40, 0, 0x80), DirectionMax);

            return true;
        }
        else if (parameterName.Equals(nameof(InputMovementSpeed), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(Math.Min(InputMovementSpeed, SpeedMax), SpeedMax);

            return true;
        }

        return base.TrySync(parameterName, bitStream);
    }
}
