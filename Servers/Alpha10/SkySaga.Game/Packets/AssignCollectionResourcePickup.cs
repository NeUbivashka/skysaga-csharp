namespace SkySaga.Game.Packets;

public static class AssignCollectionResourcePickup
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var pickupId)) return false;

        ResourcePickups.Assign(connection, (int)pickupId);

        return true;
    }
}
