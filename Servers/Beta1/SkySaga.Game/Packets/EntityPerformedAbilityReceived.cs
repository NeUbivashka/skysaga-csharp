namespace SkySaga.Game.Packets;

public static class EntityPerformedAbilityReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out _)) return false;
        if (!bitStream.ReadFullWidthUInt32(out _)) return false;

        return true;
    }
}
