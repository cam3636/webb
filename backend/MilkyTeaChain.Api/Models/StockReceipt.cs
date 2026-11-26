namespace MilkyTeaChain.Api.Models;

public class StockReceipt
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = string.Empty;

    public Branch? Branch { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
}
