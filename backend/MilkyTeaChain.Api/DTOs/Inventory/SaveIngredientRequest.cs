using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Inventory;

public class SaveIngredientRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(g|ml)$", ErrorMessage = "Đơn vị tính chỉ chấp nhận 'g' hoặc 'ml'.")]
    public string Unit { get; set; } = "g";
}
