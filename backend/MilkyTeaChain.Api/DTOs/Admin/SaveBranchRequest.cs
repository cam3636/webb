using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Admin;

public class SaveBranchRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = "07:00 - 22:30";
    public bool? IsActive { get; set; }
}
