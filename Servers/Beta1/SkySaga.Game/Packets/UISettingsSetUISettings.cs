namespace SkySaga.Game.Packets;

public static class UISettingsSetUISettings
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var autoQuest)) return false;
        if (!bitStream.ReadInt32(1, out var social)) return false;
        if (!bitStream.ReadInt32(1, out var mysteryBox)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.DisplayTimeModeMax, out var timeMode)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.QuestTrackerSortMax, out var questSort)) return false;
        if (!bitStream.ReadInt32(1, out var cursorToggle)) return false;
        if (!bitStream.ReadInt32(1, out var autoEquip)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            settings.DisableAutoQuestTracking = autoQuest != 0;
            settings.DisableSocialNotifications = social != 0;
            settings.DisableMysteryBoxNotifications = mysteryBox != 0;
            settings.DisplayTimeMode = (uint)timeMode;
            settings.QuestTrackerSort = (uint)questSort;
            settings.DisableRequestCursorToggle = cursorToggle != 0;
            settings.AutoEquipOnHotbarAssignment = autoEquip != 0;
        }

        Console.WriteLine($"[UISettings] autoQuestOff={autoQuest} socialOff={social} mysteryBoxOff={mysteryBox} "
            + $"timeMode={timeMode} questSort={questSort} cursorToggleOff={cursorToggle} autoEquip={autoEquip}");

        return true;
    }
}
