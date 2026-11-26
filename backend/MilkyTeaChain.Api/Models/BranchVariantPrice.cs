namespace MilkyTeaChain.Api.Models;

public class BranchVariantPrice
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}
