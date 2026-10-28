using System.Linq;

namespace SkySaga.Game.Components;

public class FeatureUnlockComponent : Component
{
    private const uint CountMax = 62;

    public List<string> UnlockedFeatures { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals("UnlockedFeatureCRCSet", StringComparison.OrdinalIgnoreCase))
            return false;

        var crcs = UnlockedFeatures.Select(feature => Util.ComputeCrc32(feature)).Distinct().Order().ToList();

        bitStream.WriteEscapedCount((uint)crcs.Count, CountMax);

        foreach (var crc in crcs)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(crc);
        }

        return true;
    }
}
