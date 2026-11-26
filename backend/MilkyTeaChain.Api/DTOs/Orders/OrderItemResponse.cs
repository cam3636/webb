namespace MilkyTeaChain.Api.DTOs.Orders;

public class OrderItemResponse
{
    public int Id { get; set; }
    public int VariantId { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public List<string> Toppings { get; set; } = new();
}
