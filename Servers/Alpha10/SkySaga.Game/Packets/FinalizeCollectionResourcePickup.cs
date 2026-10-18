namespace SkySaga.Game.Packets;

public static class FinalizeCollectionResourcePickup
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var pickupId)) return false;

        ResourcePickups.Finalize(connection, (int)pickupId);

        return true;
    }
}
