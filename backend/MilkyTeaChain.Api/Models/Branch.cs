namespace MilkyTeaChain.Api.Models;

public class Branch
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = "07:00 - 22:30";
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<BranchInventory> Inventories { get; set; } = new List<BranchInventory>();
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<BranchVariantPrice> VariantPrices { get; set; } = new List<BranchVariantPrice>();
    public ICollection<BranchToppingPrice> ToppingPrices { get; set; } = new List<BranchToppingPrice>();
}
