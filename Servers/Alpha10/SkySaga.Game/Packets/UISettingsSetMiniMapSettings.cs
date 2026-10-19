using System.Linq;

namespace SkySaga.Game.Packets;

public static class UISettingsSetMiniMapSettings
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (bitStream.GetNumberOfUnreadBits() < 2)
            return false;

        var first = bitStream.ReadBit();
        var second = bitStream.ReadBit();

        if (!bitStream.ReadInt32(ClientSettingsComponent.MiniMapTogglesMax, out var shortCount))
            return false;

        var count = (uint)shortCount;

        if (count == ClientSettingsComponent.MiniMapTogglesMax && bitStream.ReadBit() && !bitStream.ReadFullWidthUInt32(out count))
            return false;

        if (count > bitStream.GetNumberOfUnreadBits())
            return false;

        List<bool> toggles = [];

        for (var i = 0; i < count; i++)
            toggles.Add(bitStream.ReadBit());

        if (!bitStream.ReadInt32(ClientSettingsComponent.MiniMapValueMax, out var value))
            return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
            settings.MiniMap = new MiniMapSettings(first, second, toggles, (uint)value);

        Console.WriteLine($"[UISettings] minimap {first} {second} [{string.Concat(toggles.Select(toggle => toggle ? '1' : '0'))}] {value}");
        return true;
    }
}
