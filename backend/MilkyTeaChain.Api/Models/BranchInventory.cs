using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.Models;

public class BranchInventory
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Branch? Branch { get; set; }
    public Ingredient? Ingredient { get; set; }
}
