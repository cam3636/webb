using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.Models;

public class Order
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int CashierId { get; set; }
    public int? CustomerId { get; set; }
    public int PointsEarned { get; set; } = 0;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // Trạng thái pha chế 3 bước: "Waiting" (Chờ pha), "Preparing" (Đang pha), "Completed" (Hoàn thành)
    public string PreparationStage { get; set; } = "Waiting";
    public bool IsPrepared { get; set; } = false;
    public DateTime? PreparedAt { get; set; }

    public Branch? Branch { get; set; }
    public User? Cashier { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
