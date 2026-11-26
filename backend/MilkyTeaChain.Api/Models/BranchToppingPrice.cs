namespace MilkyTeaChain.Api.Models;

public class BranchToppingPrice
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ToppingId { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public Topping? Topping { get; set; }
}
