using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Menu;

public class SaveToppingRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
}
