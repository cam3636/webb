using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Menu;

public class SaveProductRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}
