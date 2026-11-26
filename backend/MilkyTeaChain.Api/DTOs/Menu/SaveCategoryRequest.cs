using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Menu;

public class SaveCategoryRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
