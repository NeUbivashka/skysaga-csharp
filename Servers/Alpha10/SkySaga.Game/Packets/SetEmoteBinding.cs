namespace SkySaga.Game.Packets;

public static class SetEmoteBinding
{
    private const uint SlotMax = 8;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(SlotMax, out var slot) || !bitStream.ReadInt32(EmoteTable.Max, out var emote))
            return false;

        if (slot >= ClientEmoteComponent.KeyboundSlots
            || !connection.PlayerEntity.TryGetComponent<ClientEmoteComponent>(out var emotes)
            || emote != 0 && !emotes.AvailableEmotes.Contains((ushort)emote))
        {
            Console.WriteLine($"[Emote] binding slot {slot} = {EmoteTable.NameOf((ushort)emote)} rejected");
            return true;
        }

        List<ushort> keybound = [.. emotes.KeyboundEmotes];

        while (keybound.Count < ClientEmoteComponent.KeyboundSlots)
            keybound.Add(0);

        keybound[slot] = (ushort)emote;
        emotes.KeyboundEmotes = keybound;
        connection.SaveState();

        Console.WriteLine($"[Emote] slot {slot} = {EmoteTable.NameOf((ushort)emote)}");
        return true;
    }
}
