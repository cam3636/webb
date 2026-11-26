namespace MilkyTeaChain.Api.DTOs.Reports;

public class RevenueReportResponse
{
    public int? BranchId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PaidOrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<ProductSalesResponse> ProductSales { get; set; } = new();
}
