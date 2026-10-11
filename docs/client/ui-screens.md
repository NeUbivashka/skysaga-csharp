# UI screens (client 36731)

The screen manager is `[ModeGlobal + 0x103C]` (object 0xEEC bytes, ctor `FUN_00781DA0`).
- Screen instances: `mgr + 0x18 + id*4`, ids 0..0x7C.
- Open-screen bitmask: `mgr + 0xECC + (id >> 3)`, bit `id & 7`.
- `FUN_00777A00(id)`: is open. `FUN_00777A40(id, 0, arg, ..)` -> `FUN_00785820`: open with an argument (vtable +0x44 receives it).
  `FUN_00785770(id)`: instance if open, else 0.
- Every screen derives from the base widget (ctor `FUN_009A45E0(parent, &nameHash64, id, ...)`).
  Its vtable slot 3 is OnOpen, slot 4 OnClose, slot 5 Tick, slot 17 SetOpenArgs.
- Names are a 64-bit lowercase CRC (`FUN_006E4BA0`, table `0x012A2668`). The 32-bit event/animation
  hash is `FUN_006E4CA0` (MSB-first CRC32, table `0x012A2268`, init 0, lowercased).

Snapshot taken from a live client in gameplay. `?` means the name hash matched no `ui_*` string.

| Id | Name | Vtable |
|---|---|---|
| 0x02 | `ui_hud_v3a` | `0x012E1A4C` |
| 0x03 | `ui_hud_model_editor` | `0x012E127C` |
| 0x04 | `ui_tooltip_textonly` | `0x012D4B5C` |
| 0x05 | `ui_tooltip_item` | `0x012D47A0` |
| 0x06 | `ui_tooltip_recipe` | `0x012D5E3C` |
| 0x07 | `ui_tooltip_sigil` | `0x012D4AA0` |
| 0x08 | `ui_tooltip_adventure` | `0x012D2FFC` |
| 0x09 | `ui_tooltip_photos_mmo` | `0x012D48EC` |
| 0x0A | `ui_crafting_devices_v3` | `0x012DC088` |
| 0x0B | `ui_pause_menu_v3` | `0x012E9058` |
| 0x0C | `ui_leaderboards_back_v3` | `0x012E8010` |
| 0x0D | `ui_inventory_v3` | `0x012EC148` |
| 0x0E | `ui_chest_mmo` | `0x012DBA68` |
| 0x0F | `ui_confirm_dialog_v3` | `0x012D2AE8` |
| 0x10 | `ui_confirm_drop_destroy_dialog_v3` | `0x012EBCD0` |
| 0x13 | `ui_cursor` | `0x012E3F44` |
| 0x17 | `ui_ctf_hud` | `0x012F2798` |
| 0x18 | `ui_tdm_hud` | `0x012F1B48` |
| 0x19 | `ui_build_ingame` | `0x012D757C` |
| 0x1A | `ui_build_gamestart` | `0x012D7694` |
| 0x1B | `ui_pvp_teamassignment` | `0x012F1518` |
| 0x1C | `ui_ctf_players` | `0x012F1C18` |
| 0x1D | `ui_tdm_players` | `0x012F1A8C` |
| 0x1E | `ui_build_lobby` | `0x012D8488` |
| 0x1F | `ui_build_voting_v2` | `0x012D91EC` |
| 0x20 | `ui_build_viewing` | `0x012D9EB8` |
| 0x21 | `ui_build_prize` | `0x012DA1E0` |
| 0x22 | `ui_buildoff_results` | `0x012D9DD8` |
| 0x23 | `ui_build_finished` | `0x012D7714` |
| 0x24 | `ui_build_ingame` | `0x012D7600` |
| 0x25 | `ui_build_voting_popup` | `0x012D77A8` |
| 0x26 | `?` | `0x012F17E4` |
| 0x27 | `ui_pvp_spectator` | `0x012F19C8` |
| 0x28 | `?` | `0x012F1674` |
| 0x29 | `ui_dead_message_mmo` | `0x012E9CF0` |
| 0x2A | `ui_webservice_error_v3` | `0x012DFE8C` |
| 0x2B | `ui_campfire` | `0x012DD2C0` |
| 0x2C | `ui_chat_v3` | `0x012EAC68` |
| 0x2E | `ui_quit_game_v3` | `0x012DFF34` |
| 0x2F | `ui_disconnected_v3` | `0x012DF028` |
| 0x30 | `ui_versionNumber` | `0x012E3FF0` |
| 0x31 | `ui_photo_viewer` | `0x012EAD44` |
| 0x32 | `ui_photo_viewfinder` | `0x012EE620` |
| 0x33 | `ui_confirm_photo` | `0x012DEF44` |
| 0x34 | `ui_stack_splitting_v3` | `0x012E0150` |
| 0x35 | `ui_accountSetup_Back` | `0x012DA644` |
| 0x38 | `ui_mailbox_v3` | `0x012DE0CC` |
| 0x39 | `ui_photoShare_socialMedia` | `0x012F42C4` |
| 0x3A | `?` | `0x012F41BC` |
| 0x3B | `ui_npcquests_back_v3` | `0x012EF10C` |
| 0x3C | `ui_profile_back_mmo` | `0x012F12D0` |
| 0x3D | `ui_social_heroes_v3` | `0x012EE13C` |
| 0x3E | `ui_MOTD` | `0x012EAEF8` |
| 0x3F | `?` | `0x012ED064` |
| 0x40 | `ui_narrative_delivery_v3TEMP` | `0x012EB534` |
| 0x41 | `ui_npcmysterybox` | `0x012EF3F4` |
| 0x42 | `ui_portalfade` | `0x012E444C` |
| 0x43 | `ui_npcTradeIn` | `0x012F03F8` |
| 0x44 | `ui_minimap_v3` | `0x012E5D40` |
| 0x45 | `ui_loading_screen` | `0x012E8BFC` |
| 0x46 | `?` | `0x012E4530` |
| 0x47 | `ui_destSelect_BG_v3` | `0x012DB894` |
| 0x48 | `ui_blueprints_creation_back` | `0x012D713C` |
| 0x49 | `ui_blueprints_construction_back` | `0x012D6434` |
| 0x4A | `ui_blueprints_confirm_dialog_v3` | `0x012DED24` |
| 0x4B | `ui_blueprints_shop_back` | `0x012D73BC` |
| 0x4C | `?` | `0x012E11E4` |
| 0x4E | `ui_popup_guildInvite` | `0x012DFD88` |
| 0x4F | `ui_gamequeue_invite_dialog_v3` | `0x012DF150` |
| 0x50 | `ui_groupFollow_dialog_v3` | `0x012DF22C` |
| 0x51 | `ui_hud_timer_race` | `0x012E435C` |
| 0x52 | `ui_hud_timer_endgame` | `0x012E4160` |
| 0x53 | `ui_logic_network_pin_select` | `0x012DEC08` |
| 0x54 | `ui_hud_popupmessage_v3` | `0x012E3E18` |
| 0x55 | `ui_hud_questTracker_v3a` | `0x012E49BC` |
| 0x56 | `ui_narrative_choice_temp` | `0x012EB6E0` |
| 0x57 | `ui_cutscene` | `0x012DAC10` |
| 0x58 | `ui_hud_group_v3` | `0x012E6C08` |
| 0x59 | `ui_currencyConverter_mmo` | `0x012DAAA8` |
| 0x5A | `ui_xpBar_v3` | `0x012E2750` |
| 0x5B | `ui_gamequeue_queueIcon` | `0x012E34A4` |
| 0x5C | `ui_hud_reticule` | `0x012E3D04` |
| 0x5D | `ui_hud_directional_indicator` | `0x012E3724` |
| 0x5E | `ui_hud_immobilise_warning` | `0x012E3334` |
| 0x5F | `ui_questLog_v3` | `0x012F3788` |
| 0x60 | `ui_group_management_mmo` | `0x012ECF7C` |
| 0x61 | `ui_ugc_island_title_requirements` | `0x012EB980` |
| 0x62 | `ui_logic_3DLetterSelect` | `0x012DE9DC` |
| 0x63 | `ui_teleportPending` | `0x012E6D3C` |
| 0x64 | `?` | `0x012F40A8` |
| 0x65 | `ui_tooltip_islands_mmo` | `0x012D33C8` |
| 0x66 | `ui_tooltip_blueprints_mmo` | `0x012D31CC` |
| 0x67 | `ui_shop_buyMultiples_dialog` | `0x012E0600` |
| 0x68 | `ui_confirm_gethc_dialog` | `0x012DEE0C` |
| 0x69 | `ui_chest_mmo` | `0x012EC264` |
| 0x6A | `ui_fullScreenVideo_mmo` | `0x012EBA94` |
| 0x6B | `ui_animatedPrompt_v3` | `0x012E8684` |
| 0x6C | `ui_questLog_v3_details` | `0x012F3FC8` |
| 0x6D | `ui_mailbox_v3_message` | `0x012DE64C` |
| 0x6E | `ui_crafting_recipe_v3` | `0x012DC75C` |
| 0x6F | `ui_island_backplate_v3` | `0x012E7384` |
| 0x70 | `ui_solesurvivor_hud` | `0x012F33D0` |
| 0x71 | `ui_ctf_players` | `0x012F2868` |
| 0x72 | `ui_startScreen` | `0x012E6E30` |
| 0x73 | `ui_firework_launcher_v3` | `0x012DBB94` |
| 0x74 | `ui_repair_v3` | `0x012EF744` |
| 0x75 | `?` | `0x012EBBC0` |
| 0x76 | `ui_crafting_craftMultiples_dialog` | `0x012E034C` |
| 0x77 | `ui_infoBox_dialog_v3` | `0x012E06C4` |
| 0x78 | `ui_islandBrowser_backplate` | `0x012F5220` |
| 0x79 | `ui_photoBrowser_backplate_mmo` | `0x012F5438` |
| 0x7A | `ui_blueprintBrowser_backplate` | `0x012F45B8` |
| 0x7B | `ui_overhead_icons` | `0x012E6F64` |
| 0x7C | `ui_hud_mailIcon_v3` | `0x012E713C` |
