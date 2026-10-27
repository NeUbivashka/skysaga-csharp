namespace SkySaga.Game.Packets;

public static class SetCurrentEmote
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId) || !bitStream.ReadInt32(EmoteTable.Max, out var emote))
            return false;

        if (entityId != connection.PlayerEntity.Id
            || !connection.PlayerEntity.TryGetComponent<ClientEmoteComponent>(out var emotes)
            || emote != 0 && !emotes.AvailableEmotes.Contains((ushort)emote))
        {
            Console.WriteLine($"[Emote] {entityId} emote {EmoteTable.NameOf((ushort)emote)} rejected");
            return true;
        }

        emotes.EmoteId = (ushort)emote;

        if (emote != 0)
            emotes.EmoteTriggered++;

        Console.WriteLine($"[Emote] {entityId} emote {(emote == 0 ? "stopped" : EmoteTable.NameOf((ushort)emote))}");
        return true;
    }
}
