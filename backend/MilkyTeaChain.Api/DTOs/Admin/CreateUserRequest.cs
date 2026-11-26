using System.ComponentModel.DataAnnotations;
using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.DTOs.Admin;

public class CreateUserRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    public int? BranchId { get; set; }
}
