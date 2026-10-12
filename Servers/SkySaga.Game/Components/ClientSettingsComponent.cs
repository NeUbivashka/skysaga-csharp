namespace SkySaga.Game.Components;

/// <summary>
/// The player's UI, camera and control settings. The client changes them through the
/// RPCUISettings* calls and expects them back through sync. Client bindings object vtable
/// 0x013456C0, read function FUN_00C265A0:
///
///   binding  parameter                   wire
///   -------  --------------------------  ------------------------------------------------------
///      0     hotbarslotresources         count escaped at 6 (3 bits, FUN_00C032A0), then per slot
///                                        (FUN_00C26E70): a full-width u32 (sent as the slot number)
///                                        and two item references, left hand then right hand
///                                        (FUN_00C26D30 -> FUN_007E9440)
///      1     lastactivatedslot           ranged max 5 (3 bits, FUN_00C25D30)
///      2     currentgameplaycamerastate  1 bit third-person camera (0 = first person, the
///                                        client's default), then a state ranged max 1 (1 bit,
///                                        meaning unknown) (FUN_00C25E30 / FUN_00C25C50)
///      3     minimapsettings             not sent yet
///      4     uisettings                  3 bools (DisableAutoQuestTracking, DisableSocialNotifications,
///                                        DisableMysteryBoxNotifications), DisplayTimeMode ranged max 2
///                                        (2 bits), QuestTrackerSort ranged max 4 (3 bits)
///                                        (FUN_00C27030, names from the RPC printer FUN_009C4AF0)
///      5     controlsettings             1 bit
/// </summary>
public class ClientSettingsComponent : Component
{
    public const uint DisplayTimeModeMax = 2;
    public const uint QuestTrackerSortMax = 4;
    public const uint CameraStateMax = 1;
    public const uint HotbarSlotCount = 6;
    public const uint LastActivatedSlotMax = HotbarSlotCount - 1;

    /// <summary>Hotbar items per slot: [slot * 2] = left hand, [slot * 2 + 1] = right hand.</summary>
    public InventoryItemRef?[] HotbarItems { get; set => SetIfChanged(ref field, value); } = new InventoryItemRef?[HotbarSlotCount * 2];

    public uint LastActivatedSlot { get; set => SetIfChanged(ref field, Math.Min(value, LastActivatedSlotMax)); }

    /// <summary>Put an item (or null) on a hotbar slot; hand 0 = left, 1 = right.</summary>
    public void SetHotbarItem(uint slot, uint hand, InventoryItemRef? item)
    {
        if (slot >= HotbarSlotCount || hand > 1)
            return;

        var items = (InventoryItemRef?[])HotbarItems.Clone();
        items[slot * 2 + hand] = item is { Kind: not 0 } ? item : null;
        HotbarItems = items;
    }

    public bool DisableAutoQuestTracking { get; set => SetIfChanged(ref field, value); }
    public bool DisableSocialNotifications { get; set => SetIfChanged(ref field, value); }
    public bool DisableMysteryBoxNotifications { get; set => SetIfChanged(ref field, value); }
    public uint DisplayTimeMode { get; set => SetIfChanged(ref field, Math.Min(value, DisplayTimeModeMax)); }
    public uint QuestTrackerSort { get; set => SetIfChanged(ref field, Math.Min(value, QuestTrackerSortMax)); }

    /// <summary>Third-person gameplay camera. Defaults on: in first person the player never sees their character.</summary>
    public bool ThirdPersonCamera { get; set => SetIfChanged(ref field, value); } = true;
    public uint CameraState { get; set => SetIfChanged(ref field, Math.Min(value, CameraStateMax)); }

    public bool ControlFlag { get; set => SetIfChanged(ref field, value); }

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

        if (parameterName.Equals("ControlSettings", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(ControlFlag);

            return true;
        }

        return false;
    }
}
