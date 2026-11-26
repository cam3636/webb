using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Menu;

public class SaveVariantRequest
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    public string Size { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
}
