using System.ComponentModel.DataAnnotations;

namespace MilkyTeaChain.Api.DTOs.Customers;

public class SaveCustomerRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;
}
