namespace SkySaga.Game.Packets;

public static class EntityPerformedAbilityReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var itemId)) return false;

        connection.LastUsedItemId = (int)itemId;
        connection.LastPerformedAbilityTime = DateTime.UtcNow;

        var length = (int)bitStream.GetNumberOfBytesUsed();
        if (length >= 9)
        {
            var data = bitStream.GetData();
            var forwardBuffer = new byte[length];
            Array.Copy(data, 0, forwardBuffer, 0, length);

            // Re-stamp EntityId with this player's entity id (big-endian)
            var entityIdBytes = BitConverter.GetBytes((uint)connection.PlayerEntity.Id);
            if (BitConverter.IsLittleEndian) Array.Reverse(entityIdBytes);
            Array.Copy(entityIdBytes, 0, forwardBuffer, 1, 4);

            // Set ItemId to 0 (big-endian) so other players safely skip looking up private item entity
            forwardBuffer[5] = 0;
            forwardBuffer[6] = 0;
            forwardBuffer[7] = 0;
            forwardBuffer[8] = 0;

            using var outStream = new BitStream(forwardBuffer, (uint)length, true);
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, outStream);

            Console.WriteLine($"[EntityPerformedAbility] Rebroadcast ability from player {connection.PlayerEntity.Id} to others (len={length})");
        }

        return true;
    }
}
