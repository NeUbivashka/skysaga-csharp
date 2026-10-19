namespace SkySaga.Game.Managers.Player;

public static class JobExperience
{
    public static bool Award(PlayerConnection connection, uint amount, int? jobChallengeIndex = null, uint? targetCrc = null)
    {
        if (amount == 0
            || !JobTable.TryGet(JobTable.HudJob, out var job)
            || !connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank)
            || jobRank.AddExperience(job.Name, amount) is not var (before, after))
            return false;

        connection.Send(new XPGained
        {
            EntityId = connection.PlayerEntity.Id,
            Experience = amount,
            JobChallengeIndex = jobChallengeIndex,
            JobIndex = job.Index,
            TargetCrc = targetCrc
        });

        connection.SaveState();

        Console.WriteLine($"[XP] +{amount} {job.Name}: rank {before.Rank} {before.Experience} -> rank {after.Rank} {after.Experience}");
        return true;
    }
}
