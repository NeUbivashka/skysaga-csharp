namespace SkySaga.Game.Components;

public record MiniMapSettings(bool First, bool Second, List<bool> Toggles, uint Value);

public class ClientSettingsComponent : Component
{
    public const uint DisplayTimeModeMax = 2;
    public const uint QuestTrackerSortMax = 4;
    public const uint CameraStateMax = 1;
    public const uint HotbarSlotCount = 6;
    public const uint LastActivatedSlotMax = HotbarSlotCount - 1;

    public InventoryItemRef?[] HotbarItems { get; set => SetIfChanged(ref field, value, "HotbarSlotResources"); } = new InventoryItemRef?[HotbarSlotCount * 2];

    public uint LastActivatedSlot { get; set => SetIfChanged(ref field, Math.Min(value, LastActivatedSlotMax)); }

    public void SetHotbarItem(uint slot, uint hand, InventoryItemRef? item)
    {
        if (slot >= HotbarSlotCount || hand > 1)
            return;

        var items = (InventoryItemRef?[])HotbarItems.Clone();
        items[slot * 2 + hand] = item is { Kind: not 0 } ? item : null;
        HotbarItems = items;
    }

    public bool DisableAutoQuestTracking { get; set => SetIfChanged(ref field, value, "UISettings"); }
    public bool DisableSocialNotifications { get; set => SetIfChanged(ref field, value, "UISettings"); }
    public bool DisableMysteryBoxNotifications { get; set => SetIfChanged(ref field, value, "UISettings"); }
    public uint DisplayTimeMode { get; set => SetIfChanged(ref field, Math.Min(value, DisplayTimeModeMax), "UISettings"); }
    public uint QuestTrackerSort { get; set => SetIfChanged(ref field, Math.Min(value, QuestTrackerSortMax), "UISettings"); }

    public bool ThirdPersonCamera { get; set => SetIfChanged(ref field, value, "CurrentGameplayCameraState"); } = true;
    public uint CameraState { get; set => SetIfChanged(ref field, Math.Min(value, CameraStateMax), "CurrentGameplayCameraState"); }

    public bool ControlFlag { get; set => SetIfChanged(ref field, value, "ControlSettings"); }

    public const uint MiniMapTogglesMax = 25;
    public const uint MiniMapValueMax = 32;

    public MiniMapSettings MiniMap { get; set => SetIfChanged(ref field, value, "MiniMapSettings"); } = new(false, false, [], 0);

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals("HotbarSlotResources", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount(HotbarSlotCount, HotbarSlotCount);

            for (var slot = 0u; slot < HotbarSlotCount; slot++)
            {
                bitStream.WriteFullWidthUInt32(slot);
                (HotbarItems[slot * 2] ?? new InventoryItemRef()).Encode(bitStream);
                (HotbarItems[slot * 2 + 1] ?? new InventoryItemRef()).Encode(bitStream);
            }

            return true;
        }

        if (parameterName.Equals(nameof(LastActivatedSlot), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(LastActivatedSlot, LastActivatedSlotMax);

            return true;
        }

        if (parameterName.Equals("CurrentGameplayCameraState", StringComparison.OrdinalIgnoreCase))
        {
            // Camera mode is local UI state. Sending one player's setting to a
            // different client makes that client switch perspective too.
            if (!SyncingForOwner)
                return false;

            bitStream.Write(ThirdPersonCamera);
            bitStream.WriteRanged(CameraState, CameraStateMax);

            return true;
        }

        if (parameterName.Equals("UISettings", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(DisableAutoQuestTracking);
            bitStream.Write(DisableSocialNotifications);
            bitStream.Write(DisableMysteryBoxNotifications);
            bitStream.WriteRanged(DisplayTimeMode, DisplayTimeModeMax);
            bitStream.WriteRanged(QuestTrackerSort, QuestTrackerSortMax);

            return true;
        }

        if (parameterName.Equals("MiniMapSettings", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(MiniMap.First);
            bitStream.Write(MiniMap.Second);
            bitStream.WriteEscapedCount((uint)MiniMap.Toggles.Count, MiniMapTogglesMax);

            foreach (var toggle in MiniMap.Toggles)
                bitStream.Write(toggle);

            bitStream.WriteRanged(Math.Min(MiniMap.Value, MiniMapValueMax), MiniMapValueMax);

            return true;
        }

        if (parameterName.Equals("ControlSettings", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(ControlFlag);

            return true;
        }

        return false;
    }
}
