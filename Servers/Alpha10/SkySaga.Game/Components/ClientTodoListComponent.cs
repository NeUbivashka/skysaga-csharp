namespace SkySaga.Game.Components;

public record TodoTask(byte Id, string JobChallenge, bool Complete, ulong Timestamp);

public class ClientTodoListComponent : Component
{
    private const uint TodoTaskListEscape = 64;
    private const uint TaskIdMax = 255;

    public List<TodoTask> TodoTaskList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(TodoTaskList), StringComparison.OrdinalIgnoreCase))
            return false;

        bitStream.WriteEscapedCount((uint)TodoTaskList.Count, TodoTaskListEscape);

        foreach (var task in TodoTaskList)
        {
            bitStream.WriteRanged(task.Id, TaskIdMax);
            bitStream.Write(task.Complete);
            bitStream.Write0();
            bitStream.Write1();
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(Util.ComputeCrc32(task.JobChallenge));
            bitStream.WriteUInt64(task.Timestamp);
            bitStream.Write0();
        }

        return true;
    }
}
