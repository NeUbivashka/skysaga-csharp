using SkySaga.Game.Extensions;

namespace SkySaga.Game.Components;

/// <summary>
/// Client-side state used to build an adventure's generated display name.
/// The client template calls the second value a CRC, but its packet reader
/// actually consumes a ranged CreatureSet table index.
/// </summary>
public sealed class ClientAdventureNameComponent : Component
{
    private const uint CreatureSetCount = 46;

    public uint RandomSeed
    {
        get;
        set => SetIfChanged(ref field, value);
    }

    public uint PrimaryFeatureCreatureSetCrc
    {
        get;
        set => SetIfChanged(ref field, value);
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(RandomSeed), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteFullWidthUInt32(RandomSeed);
            return true;
        }

        if (parameterName.Equals(nameof(PrimaryFeatureCreatureSetCrc), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRangedIndex(PrimaryFeatureCreatureSetCrc, CreatureSetCount);
            return true;
        }

        return false;
    }
}
