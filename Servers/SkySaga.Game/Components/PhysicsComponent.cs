namespace SkySaga.Game.Components;

public class PhysicsComponent : Component
{
    public bool FineGrainCollisionOnly { get; set => SetIfChanged(ref field, value); }
    public bool IsMoveable { get; set => SetIfChanged(ref field, value); } = true;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(FineGrainCollisionOnly), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(FineGrainCollisionOnly);

            return true;
        }
        else if (parameterName.Equals(nameof(IsMoveable), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsMoveable);

            return true;
        }

        return false;
    }
}