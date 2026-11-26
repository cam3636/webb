using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Orders;

public class CreateOrderItemRequest
{
    public int VariantId { get; set; }

    public int ProductVariantId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;

    public List<int> ToppingIds { get; set; } = new();

    public string Note { get; set; } = string.Empty;

    // Đơn giá dự kiến trên giỏ hàng của thu ngân để kiểm tra nếu giá đổi trong lúc đang mở giỏ
    public decimal? ExpectedUnitPrice { get; set; }
    public decimal? ExpectedToppingTotal { get; set; }
}
