using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public enum AiBehaviour
{
    Melee,
    Ranged,
    Taunt,
    CloseRange,
    SensoryEventResponse,
    Immobilise,
    Teleport,
    Spawn
}

public readonly record struct SelectionContext(AiBehaviour Behaviour, float Distance, bool CanPath, string[] TargetTags);

public static class AbilitySelection
{
    private const string MeleeTag = "ActionMelee";

    public static List<CreatureAbility> Select(CreatureAi ai, SelectionContext context)
    {
        if (ai.Selection is null || !CreatureAiTable.TryGetSelectionTable(ai.Selection, out var table))
            return [.. ai.Tagged(MeleeTag)];

        var selected = new List<CreatureAbility>();

        foreach (var tableRule in table)
        {
            if (!Evaluate(ai, context, tableRule.Condition) || !CreatureAiTable.TryGetSelectionList(tableRule.List, out var list))
                continue;

            foreach (var rule in list)
                if (Evaluate(ai, context, rule.Condition) == rule.SelectIfTrue)
                    selected.AddRange(WithAnyTag(ai, rule.Tags).Where(ability => !selected.Contains(ability)));
        }

        return selected;
    }

    public static bool Evaluate(CreatureAi ai, SelectionContext context, string reference)
    {
        var invert = reference.StartsWith('!');
        var name = invert ? reference[1..] : reference;

        if (!CreatureAiTable.TryGetCondition(name, out var condition))
            return invert;

        var value = condition.Kind switch
        {
            "behaviour" => Enum.TryParse<AiBehaviour>(condition.Argument, out var behaviour) && behaviour == context.Behaviour,
            "inrange" => WithAnyTag(ai, condition.Argument.Split(',')).Any(ability => context.Distance <= ability.AttackRange),
            "canpath" => context.CanPath,
            "targetusing:TargetEntity" => condition.Argument.Split(',').Any(tag => context.TargetTags.Contains(tag, StringComparer.OrdinalIgnoreCase)),
            "constant" => condition.Argument == "True",
            "and" => condition.Argument.Split(',').All(part => Evaluate(ai, context, part)),
            "or" => condition.Argument.Split(',').Any(part => Evaluate(ai, context, part)),
            _ => false
        };

        return value != condition.Invert != invert;
    }

    private static IEnumerable<CreatureAbility> WithAnyTag(CreatureAi ai, string[] tags)
        => ai.Abilities.Where(ability => tags.Any(ability.HasTag));
}
