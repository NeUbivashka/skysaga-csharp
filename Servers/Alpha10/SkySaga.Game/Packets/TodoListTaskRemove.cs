namespace SkySaga.Game.Packets;

public static class TodoListTaskRemove
{
    private const byte TaskIdMax = 255;
    private const byte ReasonMax = 11;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadByte(TaskIdMax, out var taskId) || !bitStream.ReadByte(ReasonMax, out var reason))
            return false;

        JobChallengeProgress.Cancel(connection, taskId, reason);
        return true;
    }
}
