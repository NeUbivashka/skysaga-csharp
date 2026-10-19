using System.Linq;

namespace SkySaga.Game.Components;

public class ClientEmoteComponent : Component
{
    public const int KeyboundSlots = 8;
    private const uint ScrollsMax = 0x100;
    private const uint AvailableEscape = 0x100;

    public ushort EmoteId { get; set => SetIfChanged(ref field, value); }

    public byte EmoteTriggered { get; set => SetIfChanged(ref field, value); }

    public List<ushort> AvailableEmotes { get; set => SetIfChanged(ref field, value); } = [];

    public uint NumberOfEmoteScrollsUsed { get; set => SetIfChanged(ref field, value); }

    public List<ushort> KeyboundEmotes { get; set => SetIfChanged(ref field, value); } = [.. Enumerable.Repeat((ushort)0, KeyboundSlots)];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(EmoteId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(EmoteId, EmoteTable.Max);
            return true;
        }

        if (parameterName.Equals(nameof(EmoteTriggered), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(EmoteTriggered);
            return true;
        }

        if (parameterName.Equals(nameof(AvailableEmotes), StringComparison.OrdinalIgnoreCase))
        {
            WriteList(bitStream, AvailableEmotes, AvailableEscape);
            return true;
        }

        if (parameterName.Equals(nameof(NumberOfEmoteScrollsUsed), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(Math.Min(NumberOfEmoteScrollsUsed, ScrollsMax), ScrollsMax);
            return true;
        }

        if (parameterName.Equals(nameof(KeyboundEmotes), StringComparison.OrdinalIgnoreCase))
        {
            WriteList(bitStream, KeyboundEmotes, KeyboundSlots);
            return true;
        }

        return false;
    }

    private static void WriteList(BitStream bitStream, List<ushort> emotes, uint escape)
    {
        bitStream.WriteEscapedCount((uint)emotes.Count, escape);

        foreach (var emote in emotes)
            bitStream.WriteRanged(emote, EmoteTable.Max);
    }
}
