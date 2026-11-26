using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Orders;

public class CreateOrderRequest
{
    public int? BranchId { get; set; }
    public int? CustomerId { get; set; }
    public bool ConfirmPriceChange { get; set; } = false;

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}
