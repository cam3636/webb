namespace MilkyTeaChain.Api.DTOs.Orders;

public class OrderResponse
{
    public int OrderId { get; set; }
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCity { get; set; } = string.Empty;
    public string BranchAddress { get; set; } = string.Empty;
    public string BranchPhone { get; set; } = string.Empty;
    public int CashierId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int PointsEarned { get; set; }
    public int? CustomerTotalPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPrepared { get; set; }
    public string PreparationStatus { get; set; } = "Waiting";
    public DateTime? PreparedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}
