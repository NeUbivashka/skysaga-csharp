namespace SkySaga.Game.Packets;

/// <summary>
/// RPCUISettingsSetUISettings, ordinal 21, client to server (send stub FUN_009C4420, writer
/// FUN_009C4DE0, JSON printer FUN_009C4AF0). One byte:
///
///   DisableAutoQuestTracking         1 bit
///   DisableSocialNotifications       1 bit
///   DisableMysteryBoxNotifications   1 bit
///   DisplayTimeMode                  ranged max 2 (2 bits)
///   QuestTrackerSort                 ranged max 4 (3 bits)
///
/// Stored on ClientSettingsComponent, whose uisettings binding syncs it back.
/// </summary>
public static class UISettingsSetUISettings
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var autoQuest)) return false;
        if (!bitStream.ReadInt32(1, out var social)) return false;
        if (!bitStream.ReadInt32(1, out var mysteryBox)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.DisplayTimeModeMax, out var timeMode)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.QuestTrackerSortMax, out var questSort)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            settings.DisableAutoQuestTracking = autoQuest != 0;
            settings.DisableSocialNotifications = social != 0;
            settings.DisableMysteryBoxNotifications = mysteryBox != 0;
            settings.DisplayTimeMode = (uint)timeMode;
            settings.QuestTrackerSort = (uint)questSort;
        }

        Console.WriteLine($"[UISettings] autoQuestOff={autoQuest} socialOff={social} mysteryBoxOff={mysteryBox} "
            + $"timeMode={timeMode} questSort={questSort}");

        return true;
    }
}
