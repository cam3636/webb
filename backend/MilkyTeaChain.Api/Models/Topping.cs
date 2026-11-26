namespace MilkyTeaChain.Api.Models;

public class Topping
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ToppingRecipeItem> RecipeItems { get; set; } = new List<ToppingRecipeItem>();
}
