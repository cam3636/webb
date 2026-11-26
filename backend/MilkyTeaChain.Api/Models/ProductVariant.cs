namespace MilkyTeaChain.Api.Models;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public Product? Product { get; set; }
    public ICollection<VariantRecipeItem> RecipeItems { get; set; } = new List<VariantRecipeItem>();
}
