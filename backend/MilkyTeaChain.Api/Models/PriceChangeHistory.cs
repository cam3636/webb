namespace MilkyTeaChain.Api.Models;

public class PriceChangeHistory
{
    public int Id { get; set; }
    public string ItemType { get; set; } = string.Empty; // "Variant" hoặc "Topping"
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? BranchId { get; set; } // null = Giá chung toàn chuỗi
    public string ScopeName { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string ActionType { get; set; } = "Update"; // "SetDefault", "SetBranchOverride", "ResetToDefault"
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
