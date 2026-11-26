namespace MilkyTeaChain.Api.Models;

public class StockReceiptItem
{
    public int Id { get; set; }
    public int StockReceiptId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public StockReceipt? StockReceipt { get; set; }
    public Ingredient? Ingredient { get; set; }
}
