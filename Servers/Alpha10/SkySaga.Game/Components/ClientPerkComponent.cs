namespace SkySaga.Game.Components;

public sealed record PerkData(uint PerkCrc, uint Source, ulong ExpiryTime, uint ExpiryConditionCrc, bool InvertExpiryCondition);

public class ClientPerkComponent : Component
{
    private const uint ListEscape = 10;
    private const uint SourceMax = 5;

    private bool _listSent;

    public IReadOnlyList<PerkData> AdditionalPerkList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(AdditionalPerkList), StringComparison.OrdinalIgnoreCase)
            || (AdditionalPerkList.Count == 0 && !_listSent))
            return false;

        _listSent = AdditionalPerkList.Count > 0;

        bitStream.WriteEscapedCount((uint)AdditionalPerkList.Count, ListEscape);

        foreach (var perk in AdditionalPerkList)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(perk.PerkCrc);
            bitStream.WriteRanged(perk.Source, SourceMax);
            bitStream.WriteUInt64(perk.ExpiryTime);

            if (perk.ExpiryConditionCrc == 0)
            {
                bitStream.Write0();
            }
            else
            {
                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(perk.ExpiryConditionCrc);
            }

            bitStream.Write(perk.InvertExpiryCondition);
        }

        return true;
    }
}
