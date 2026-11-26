namespace MilkyTeaChain.Api.Models;

public class CustomerPointTransaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int OrderId { get; set; }
    public int BranchId { get; set; }
    public int PointsEarned { get; set; }
    public decimal OrderAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }
    public Order? Order { get; set; }
    public Branch? Branch { get; set; }
}
