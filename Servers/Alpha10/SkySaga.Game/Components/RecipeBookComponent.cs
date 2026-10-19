namespace SkySaga.Game.Components;

public record KnownRecipe(ushort Recipe, IReadOnlyList<ushort> Variants);

public class RecipeBookComponent : Component
{
    private const uint RecipeListEscape = 1000;
    private const uint VariantListEscape = 0x1E;
    private const uint ScrollsUsedMax = 1000;

    public List<KnownRecipe> RecipeList { get; set => SetIfChanged(ref field, value); } = [];

    public int NumberOfRecipeScrollsUsed { get; set => SetIfChanged(ref field, value); }

    public bool Knows(ushort recipe) => RecipeList.Exists(entry => entry.Recipe == recipe);

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(RecipeList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)RecipeList.Count, RecipeListEscape);

            foreach (var entry in RecipeList)
            {
                bitStream.WriteRangedIndex(entry.Recipe, RecipeTable.RecipeTableCount);
                bitStream.WriteEscapedCount((uint)entry.Variants.Count, VariantListEscape);

                foreach (var variant in entry.Variants)
                    bitStream.WriteRangedIndex(variant, RecipeTable.VariantTableCount);
            }

            return true;
        }

        if (parameterName.Equals(nameof(NumberOfRecipeScrollsUsed), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged((uint)Math.Clamp(NumberOfRecipeScrollsUsed, 0, (int)ScrollsUsedMax), ScrollsUsedMax);
            return true;
        }

        return false;
    }
}
