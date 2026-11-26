using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
