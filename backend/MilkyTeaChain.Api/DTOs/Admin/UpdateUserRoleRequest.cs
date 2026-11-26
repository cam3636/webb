using System.ComponentModel.DataAnnotations;
using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.DTOs.Admin;

public class UpdateUserRoleRequest
{
    [Required]
    public UserRole? Role { get; set; }
    public int? BranchId { get; set; }
}
