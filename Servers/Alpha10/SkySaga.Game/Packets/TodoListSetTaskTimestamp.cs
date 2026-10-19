namespace SkySaga.Game.Packets;

public static class TodoListSetTaskTimestamp
{
    private const byte TaskIdMax = 255;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadByte(TaskIdMax, out var taskId))
            return false;

        ulong timestamp = 0;

        if (bitStream.ReadBit() && !bitStream.ReadUInt64(out timestamp))
            return false;

        JobChallenges.SetTodoTimestamp(connection, taskId, timestamp);
        return true;
    }
}
