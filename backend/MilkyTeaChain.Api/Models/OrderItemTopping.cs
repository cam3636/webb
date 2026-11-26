namespace MilkyTeaChain.Api.Models;

public class OrderItemTopping
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public int ToppingId { get; set; }
    public string ToppingNameSnapshot { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public OrderItem? OrderItem { get; set; }
    public Topping? Topping { get; set; }
}
