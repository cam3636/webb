namespace MilkyTeaChain.Api.Models;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "g"; // "g" hoặc "ml"
    public bool IsActive { get; set; } = true;

    public ICollection<VariantRecipeItem> VariantRecipeItems { get; set; } = new List<VariantRecipeItem>();
    public ICollection<ToppingRecipeItem> ToppingRecipeItems { get; set; } = new List<ToppingRecipeItem>();
    public ICollection<BranchInventory> BranchInventories { get; set; } = new List<BranchInventory>();
}
