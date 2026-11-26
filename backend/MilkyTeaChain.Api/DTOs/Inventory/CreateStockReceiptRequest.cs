using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Inventory;

public class CreateStockReceiptRequest
{
    public int? BranchId { get; set; }
    public string Note { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<StockReceiptItemRequest> Items { get; set; } = new();
}
