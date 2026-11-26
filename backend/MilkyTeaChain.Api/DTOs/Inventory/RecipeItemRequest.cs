using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Inventory;

public class RecipeItemRequest
{
    [Required]
    public int IngredientId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }
}
