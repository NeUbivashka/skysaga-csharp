namespace SkySaga.Game.Packets;

/// <summary>
/// RPCQuitGame, ordinal 0, no payload, both directions.
///
/// Server to client. Reader: FUN_007FBCF0 -> FUN_00BB3E50(1, 0, ...). Verified empty: the stub
/// reads nothing and dispatches straight to the handler at 0x007D26E0, which only sets ClientNet
/// +0xE8 = 1. The client-net tick FUN_007D0430 then (once +0xEC >= +0xF0) sets
/// DAT_01440E98[0x70] = 1, the "Level unload" flag the gameplay tick FUN_00964010 polls, and bumps
/// the "ShuttingDown" stat. The world unloads and the client exits.
///
/// Client to server: sent by the pause menu's Quit button after its confirm dialog
/// (ui_pause_menu_v3 FUN_008FAD80, button hash "Quit" -> confirm callback FUN_008FB010 -> send
/// FUN_00717B40). The client does nothing else by itself; it waits for the echo. Without it the
/// button did nothing visible (2026-10-12: three QuitGame in the engine log, nothing until Alt+F4).
/// </summary>
public class QuitGame : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QuitGame);

        return bitStream;
    }

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        connection.SaveState();

        connection.Send(new QuitGame());

        Console.WriteLine("[QuitGame] Client asked to quit - state saved, QuitGame echoed back.");

        return true;
    }
}
