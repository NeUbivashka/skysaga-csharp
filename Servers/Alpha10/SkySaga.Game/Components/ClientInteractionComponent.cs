namespace SkySaga.Game.Components;

public class ClientInteractionComponent : Component
{
    private const uint QuestKeyRequirementListEscape = 0x20;
    private const uint KeyRequirementsPaidByEscape = 0x400;

    public uint? KeyRequirementCrc { get; set => SetIfChanged(ref field, value, "keyrequirementcrc"); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals("keyrequirementcrc", StringComparison.OrdinalIgnoreCase))
        {
            if (KeyRequirementCrc is uint crc)
            {
                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(crc);
            }
            else
                bitStream.Write0();

            return true;
        }

        if (parameterName.Equals("questkeyrequirementlist", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount(0, QuestKeyRequirementListEscape);
            return true;
        }

        if (parameterName.Equals("keyrequirementspaidby", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount(0, KeyRequirementsPaidByEscape);
            return true;
        }

        return false;
    }
}
