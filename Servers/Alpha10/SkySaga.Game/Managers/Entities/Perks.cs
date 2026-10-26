using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public static class Perks
{
    private sealed class ActivePerk
    {
        public required Perk Perk;
        public required PerkData Data;
        public required DateTime? Expires;
        public required PerkCondition? Condition;
        public required bool InvertCondition;
        public required List<(PeriodicPerkEffect Effect, DateTime Next)> Periodic;
        public int Hits;
    }

    private static readonly Dictionary<int, (Entity Entity, List<ActivePerk> Perks)> Active = [];
    private static readonly object Gate = new();

    public static void ApplyImpact(Entity target, IEnumerable<string> impactActions)
    {
        var precedingHits = 0;

        foreach (var action in impactActions)
        {
            if (PerkTable.TryGetAction(action, out _))
                Apply(target, action, precedingHits);
            else
                precedingHits++;
        }
    }

    public static void Apply(Entity target, string entityAction, int precedingHits = 0)
    {
        if (!PerkTable.TryGetAction(entityAction, out var entries)
            || !target.TryGetComponent<ClientPerkComponent>(out var component)
            || (target.TryGetComponent<ClientHealthComponent>(out var health) && health.IsDead))
            return;

        var now = DateTime.UtcNow;
        var clock = RakNet.RakNet.GetTime();

        lock (Gate)
        {
            if (!Active.TryGetValue(target.Id, out var state))
                Active[target.Id] = state = (target, []);

            foreach (var entry in entries)
            {
                if (!PerkTable.TryGetPerk(entry.Perk, out var perk))
                    continue;

                if (!perk.Stackable)
                    state.Perks.RemoveAll(active => active.Perk.Name.Equals(perk.Name, StringComparison.OrdinalIgnoreCase));

                PerkCondition? condition = null;

                if (entry.Condition is not null)
                    PerkTable.TryGetCondition(entry.Condition, out condition);

                state.Perks.Add(new ActivePerk
                {
                    Perk = perk,
                    Data = new PerkData(Util.ComputeCrc32(perk.Name), 0,
                        entry.DurationSeconds > 0 ? clock + (ulong)(entry.DurationSeconds * 1000) : 0,
                        entry.Condition is null ? 0 : Util.ComputeCrc32(entry.Condition), entry.InvertCondition),
                    Expires = entry.DurationSeconds > 0 ? now.AddSeconds(entry.DurationSeconds) : null,
                    Condition = condition,
                    InvertCondition = entry.InvertCondition,
                    Periodic = [.. perk.Effects
                        .Select(effect => PerkTable.TryGetPeriodic(effect, out var periodic) ? periodic : null)
                        .OfType<PeriodicPerkEffect>()
                        .Select(periodic => (periodic, now.AddSeconds(periodic.IntervalSeconds)))],
                    Hits = precedingHits
                });

                Console.WriteLine($"[Perks] {target.Name}#{target.Id} gains {perk.Name} from {entityAction}"
                    + (entry.DurationSeconds > 0 ? $" for {entry.DurationSeconds:0.##} s" : "")
                    + (entry.Condition is not null ? $" until {entry.Condition}" : ""));
            }

            Publish(component, state.Perks);
        }
    }

    public static void Hit(Entity target)
    {
        lock (Gate)
        {
            if (!Active.TryGetValue(target.Id, out var state))
                return;

            foreach (var active in state.Perks)
                active.Hits++;

            Expire(state.Entity, state.Perks, DateTime.UtcNow);
        }
    }

    public static void Clear(Entity target)
    {
        lock (Gate)
        {
            if (!Active.Remove(target.Id, out var state))
                return;

            state.Perks.Clear();

            if (target.TryGetComponent<ClientPerkComponent>(out var component))
                Publish(component, state.Perks);
        }
    }

    public static void Update(DateTime now)
    {
        lock (Gate)
        {
            foreach (var (id, (entity, perks)) in Active.ToList())
            {
                foreach (var active in perks.ToList())
                {
                    for (var i = 0; i < active.Periodic.Count; i++)
                    {
                        var (effect, next) = active.Periodic[i];

                        if (now < next)
                            continue;

                        active.Periodic[i] = (effect, next.AddSeconds(effect.IntervalSeconds));
                        Periodic(entity, active.Perk, effect);
                    }
                }

                Expire(entity, perks, now);

                if (perks.Count == 0)
                    Active.Remove(id);
            }
        }
    }

    private static void Periodic(Entity entity, Perk perk, PeriodicPerkEffect effect)
    {
        if (effect.Hp == 0 || !entity.TryGetComponent<ClientHealthComponent>(out var health) || health.IsDead)
            return;

        if (effect.Hp < 0)
            health.Damage((int)MathF.Round(-effect.Hp));
        else
            health.Heal((int)MathF.Round(effect.Hp));

        Console.WriteLine($"[Perks] {entity.Name}#{entity.Id} {perk.Name}: {effect.Action} {effect.Hp:+0;-0} hp, hp {health.SyncedFullHPValues}/{health.MaxHP}");
    }

    private static void Expire(Entity entity, List<ActivePerk> perks, DateTime now)
    {
        var dead = entity.TryGetComponent<ClientHealthComponent>(out var health) && health.IsDead;
        var removed = perks.RemoveAll(active =>
        {
            var ended = dead
                || (active.Expires is DateTime expires && now >= expires)
                || (active.Condition is not null && Holds(active.Condition, active, dead) != active.InvertCondition);

            if (ended)
                Console.WriteLine($"[Perks] {entity.Name}#{entity.Id} loses {active.Perk.Name}");

            return ended;
        });

        if (removed > 0 && entity.TryGetComponent<ClientPerkComponent>(out var component))
            Publish(component, perks);
    }

    private static bool Holds(PerkCondition condition, ActivePerk active, bool dead)
    {
        var result = condition.Event switch
        {
            "Hit" => active.Hits >= condition.Hits,
            "Death" => dead,
            null => condition.AnyOf.Any(name => PerkTable.TryGetCondition(name.TrimStart('!'), out var inner)
                && Holds(inner, active, dead) != name.StartsWith('!')),
            _ => false
        };

        return result != condition.Invert;
    }

    private static void Publish(ClientPerkComponent component, List<ActivePerk> perks)
        => component.AdditionalPerkList = [.. perks.Select(active => active.Data)];
}
