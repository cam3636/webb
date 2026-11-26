namespace MilkyTeaChain.Api.Models;

public class ToppingRecipeItem
{
    public int Id { get; set; }
    public int ToppingId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public Topping? Topping { get; set; }
    public Ingredient? Ingredient { get; set; }
}
