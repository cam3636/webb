namespace MilkyTeaChain.Api.DTOs.Reports;

public class ProductSalesResponse
{
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalSalesAmount { get; set; }
}
