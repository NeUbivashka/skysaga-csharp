using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public readonly record struct RecipeInput(string Resource, int Quantity);

public sealed record RecipeInfo(
    ushort WireIndex,
    string Name,
    string Device,
    float Seconds,
    bool NewPlayers,
    string Output,
    int OutputQuantity,
    int OutputValue,
    bool[] OutputMaterialSlots,
    RecipeInput?[] MaterialInputs,
    RecipeInput[] ExtraInputs,
    ushort[] Variants);

public readonly record struct CraftingXp(int Base, int PowerTen, int Min, int Max);

public static class RecipeTable
{
    public const string HandCrafting = "Hand_Crafting";

    // Alpha 10 recipes use Crafting_Device for the consolidated crafting UI,
    // while the client still ships the legacy station entity templates. Keep
    // those stations usable as equivalent entry points for that UI.
    private static readonly HashSet<string> UnifiedCraftingStations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Anvil", "Armoury", "Bellows", "Cauldron", "Cold_Weather_Survival_Kit",
        "Compressor", "Crafting_Device", "Desert_Survival_Kit", "Farming_Kit",
        "Forge", "Generator", "Hunters_Pack", "Husbandry_Kit", "Incubator", "Kiln",
        "Magical_Symbol", "Oven", "Potting_Table", "Workbench", "Writing_Desk"
    };

    private static readonly Lazy<(List<RecipeInfo> Recipes, uint VariantCount, CraftingXp Xp)> Data = new(Load);

    private static (List<RecipeInfo>, uint, CraftingXp) Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Recipes.txt");
        List<RecipeInfo> recipes = [];
        uint variants = 0;
        CraftingXp xp = default;

        if (!File.Exists(path))
            return (recipes, variants, xp);

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("# variants ", StringComparison.Ordinal))
                variants = uint.Parse(line["# variants ".Length..], CultureInfo.InvariantCulture);

            if (line.StartsWith("# xp ", StringComparison.Ordinal)
                && line["# xp ".Length..].Split(' ').Select(int.Parse).ToArray() is [var xpBase, var powerTen, var min, var max])
                xp = new CraftingXp(xpBase, powerTen, min, max);

            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 10)
                continue;

            var inputs = parts[8].Split(',').Select(ParseInput).ToArray();

            recipes.Add(new RecipeInfo(
                (ushort)(recipes.Count + 1),
                parts[0],
                parts[1],
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                parts[3] == "1",
                parts[4],
                int.Parse(parts[5], CultureInfo.InvariantCulture),
                int.Parse(parts[6], CultureInfo.InvariantCulture),
                [.. parts[7].Select(c => c == '1')],
                inputs[..4],
                [.. inputs[4..].OfType<RecipeInput>()],
                parts[9] == "-" ? [] : [.. parts[9].Split(',').Select(ushort.Parse)]));
        }

        return (recipes, variants, xp);
    }

    private static RecipeInput? ParseInput(string text)
    {
        if (text == "-")
            return null;

        var star = text.LastIndexOf('*');

        return new RecipeInput(text[..star], int.Parse(text[(star + 1)..], CultureInfo.InvariantCulture));
    }

    public static IReadOnlyList<RecipeInfo> All => Data.Value.Recipes;

    public static uint ExperiencePerItem(RecipeInfo recipe)
    {
        var xp = Data.Value.Xp;
        var steps = MathF.Max(MathF.Log10(recipe.OutputValue / MathF.Pow(10f, xp.PowerTen)), 1f);
        var perItem = (int)(1f / recipe.OutputQuantity * (xp.Base * MathF.Pow(2f, steps)));

        return (uint)Math.Min(Math.Max(perItem, xp.Min), xp.Max);
    }

    public static uint RecipeTableCount => (uint)Data.Value.Recipes.Count + 1;

    public static uint VariantTableCount => Data.Value.VariantCount + 1;

    public static bool SupportsDevice(string requiredDevice, string actualDevice) =>
        actualDevice.Equals(requiredDevice, StringComparison.OrdinalIgnoreCase)
        || (requiredDevice.Equals("Crafting_Device", StringComparison.OrdinalIgnoreCase)
            && UnifiedCraftingStations.Contains(actualDevice));

    public static bool TryGetByWireIndex(ushort wireIndex, [NotNullWhen(true)] out RecipeInfo? recipe)
    {
        recipe = wireIndex >= 1 && wireIndex <= Data.Value.Recipes.Count ? Data.Value.Recipes[wireIndex - 1] : null;
        return recipe is not null;
    }

    public static bool TryGetByName(string name, [NotNullWhen(true)] out RecipeInfo? recipe)
    {
        recipe = Data.Value.Recipes.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return recipe is not null;
    }

    public static bool TryGetByVariant(ushort variantWireIndex, [NotNullWhen(true)] out RecipeInfo? recipe)
    {
        recipe = Data.Value.Recipes.FirstOrDefault(entry => entry.Variants.Contains(variantWireIndex));
        return recipe is not null;
    }
}
