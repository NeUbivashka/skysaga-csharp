namespace SkySaga.Game.Components;

public class TagsComponent : Component
{
    private const uint TagCountEscape = 0x20;

    public List<uint> TagList { get; set => SetIfChanged(ref field, value); } = [];

    public List<uint> SavedTagList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(TagList), StringComparison.OrdinalIgnoreCase))
        {
            WriteTags(bitStream, TagList);
            return true;
        }

        if (parameterName.Equals(nameof(SavedTagList), StringComparison.OrdinalIgnoreCase))
        {
            WriteTags(bitStream, SavedTagList);
            return true;
        }

        return false;
    }

    private static void WriteTags(BitStream bitStream, List<uint> tags)
    {
        bitStream.WriteEscapedCount((uint)tags.Count, TagCountEscape);

        foreach (var tag in tags)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(tag);
        }
    }
}
