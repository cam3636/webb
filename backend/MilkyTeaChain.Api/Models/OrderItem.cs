namespace MilkyTeaChain.Api.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string SizeSnapshot { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Order? Order { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public ICollection<OrderItemTopping> Toppings { get; set; } = new List<OrderItemTopping>();
}
