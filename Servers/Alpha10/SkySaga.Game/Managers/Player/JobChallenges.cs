using System.Linq;

namespace SkySaga.Game.Managers.Player;

public static class JobChallenges
{
    private const string TutorialJob = "Tutorial";
    private static readonly string[] TutorialPrefixes = ["FTUE", "Explorer_Tut_", "Atlas_Enter"];

    public static List<string> TutorialCompleted()
        => [.. JobChallengeTable.All.Where(IsTutorial).Select(challenge => challenge.Name)];

    private static bool IsTutorial(JobChallengeInfo challenge)
        => challenge.Job == TutorialJob || TutorialPrefixes.Any(prefix => challenge.Name.StartsWith(prefix, StringComparison.Ordinal));

    public static List<string> RollDailies(JobInfo job, IReadOnlyCollection<string> completed, IReadOnlyCollection<ActiveJobChallenge> active)
    {
        if (job.Hidden || job.Faction != job.Name)
            return [];

        var done = new HashSet<string>(completed, StringComparer.OrdinalIgnoreCase);
        var running = new HashSet<string>(active.Select(challenge => challenge.Name), StringComparer.OrdinalIgnoreCase);

        var candidates = JobChallengeTable.All
            .Where(challenge => challenge.Dailies && !challenge.Master && challenge.InQuestLog
                && challenge.Job == job.Name
                && !running.Contains(challenge.Name)
                && (challenge.Reactivate || !done.Contains(challenge.Name))
                && ConditionTable.Holds(challenge.Condition, challenge.ConditionInvert, done.Contains, running.Contains))
            .ToList();

        return [.. candidates.OrderBy(_ => Random.Shared.Next()).Take(job.ActiveChallenges).Select(challenge => challenge.Name)];
    }

    private const string DailyFactionQuests = "dailyfactionquests";

    public static List<QuestOffer> OffersFor(IMapEntityManager entityManager, IReadOnlyList<JobProgress> jobs)
        => [.. entityManager.Entities
            .Where(entity => DailyFactionQuests.Equals(entity.TextParameter("questvendorcomponentdata"), StringComparison.OrdinalIgnoreCase))
            .Select(entity => (entity, job: jobs.FirstOrDefault(job => JobTable.TryGet(job.Job, out var info)
                && info.Faction.Equals(entity.TextParameter("faction"), StringComparison.OrdinalIgnoreCase)
                && info.Faction == info.Name)))
            .Where(pair => pair.job is not null)
            .Select(pair => new QuestOffer(pair.entity.Id, [.. pair.job!.NormalAvailable]))];

    public static List<TodoTask> TodoTasksFor(IReadOnlyList<ActiveJobChallenge> active, IReadOnlyDictionary<string, ulong> timestamps)
        => [.. active
            .Where(challenge => JobChallengeTable.TryGet(challenge.Name, out var info) && info.InQuestLog)
            .Select((challenge, index) => new TodoTask((byte)index, challenge.Name, false, timestamps.GetValueOrDefault(challenge.Name)))];

    public static bool SetTodoTimestamp(PlayerConnection connection, byte taskId, ulong timestamp)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientTodoListComponent>(out var todoList))
            return false;

        var index = todoList.TodoTaskList.FindIndex(task => task.Id == taskId);

        if (index < 0)
            return false;

        List<TodoTask> tasks = [.. todoList.TodoTaskList];
        tasks[index] = tasks[index] with { Timestamp = timestamp };
        todoList.TodoTaskList = tasks;
        connection.SaveState();

        Console.WriteLine($"[Todo] task {taskId} ({tasks[index].JobChallenge}) timestamp {timestamp}");
        return true;
    }

    private static List<TodoTask> AddTodoTask(IReadOnlyList<TodoTask> tasks, JobChallengeInfo challenge)
    {
        if (!challenge.InQuestLog || tasks.Any(task => task.JobChallenge.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase)))
            return [.. tasks];

        var id = Enumerable.Range(0, 256).First(candidate => tasks.All(task => task.Id != candidate));
        return [.. tasks, new TodoTask((byte)id, challenge.Name, false, 0)];
    }

    public static bool Activate(PlayerConnection connection, uint challengeCrc)
    {
        if (!JobChallengeTable.TryGet(challengeCrc, out var challenge)
            || !connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank))
            return false;

        var jobIndex = jobRank.JobList.FindIndex(job => job.NormalAvailable.Contains(challenge.Name, StringComparer.OrdinalIgnoreCase));

        if (jobIndex < 0 || jobRank.ActiveJobChallengeList.Any(active => active.Name.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase)))
            return false;

        List<JobProgress> jobs = [.. jobRank.JobList];
        jobs[jobIndex] = jobs[jobIndex] with
        {
            NormalAvailable = [.. jobs[jobIndex].NormalAvailable.Where(name => !name.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase))]
        };

        jobRank.JobList = jobs;
        jobRank.QuestsForEntityList = OffersFor(connection.WorldManager.EntityManager, jobs);
        jobRank.ActiveJobChallengeList = [.. jobRank.ActiveJobChallengeList, new ActiveJobChallenge(challenge.Name, 0)];

        if (connection.PlayerEntity.TryGetComponent<ClientTodoListComponent>(out var todoList))
            todoList.TodoTaskList = AddTodoTask(todoList.TodoTaskList, challenge);

        connection.SaveState();

        Console.WriteLine($"[Challenge] {challenge.Name} activated ({challenge.Action} x{challenge.Number}, {challenge.Xp} XP)");
        return true;
    }
}
