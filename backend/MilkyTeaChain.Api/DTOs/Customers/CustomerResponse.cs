namespace MilkyTeaChain.Api.DTOs.Customers;

public class CustomerResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int Points { get; set; }
    public int PurchaseCount { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
    public decimal TotalSpent { get; set; }
}
