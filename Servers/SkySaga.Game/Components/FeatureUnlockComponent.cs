namespace SkySaga.Game.Components;

public class FeatureUnlockComponent : Component
{

    public List<bool> FeatureIsLockedStatusList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(FeatureIsLockedStatusList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write0();

            for (var i = 0; i < 30; i++)
                bitStream.Write0();

            return true;
        }

        return false;
    }
}