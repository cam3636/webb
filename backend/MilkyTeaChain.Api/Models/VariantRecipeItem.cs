namespace MilkyTeaChain.Api.Models;

public class VariantRecipeItem
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public ProductVariant? ProductVariant { get; set; }
    public Ingredient? Ingredient { get; set; }
}
