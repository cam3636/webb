using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Reports;
using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.Services;

public class ReportService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public ReportService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    public async Task<RevenueReportResponse> GetRevenueReportAsync(
        ClaimsPrincipal user,
        int? requestedBranchId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var branchId = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        DateTime? startUtc = fromDate?.Date;
        DateTime? endExclusiveUtc = toDate?.Date.AddDays(1);

        var query = _context.Orders
            .Where(o => o.Status == OrderStatus.Paid && o.PaidAt != null)
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        if (startUtc.HasValue)
        {
            query = query.Where(o => o.PaidAt >= startUtc.Value);
        }

        if (endExclusiveUtc.HasValue)
        {
            query = query.Where(o => o.PaidAt < endExclusiveUtc.Value);
        }

        var paidOrderCount = await query.CountAsync();
        var totalRevenue = await query.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var averageOrderValue = paidOrderCount > 0 ? Math.Round(totalRevenue / paidOrderCount, 2) : 0m;
        var productSales = await GetTopSellingProductsAsync(user, branchId, fromDate, toDate);

        string scopeName = "Toàn chuỗi";
        if (branchId.HasValue)
        {
            var branch = await _context.Branches.FindAsync(branchId.Value);
            scopeName = branch != null ? branch.Name : $"Chi nhánh #{branchId.Value}";
        }

        return new RevenueReportResponse
        {
            BranchId = branchId,
            Scope = scopeName,
            FromDate = fromDate?.Date,
            ToDate = toDate?.Date,
            PaidOrderCount = paidOrderCount,
            TotalRevenue = totalRevenue,
            AverageOrderValue = averageOrderValue,
            ProductSales = productSales
        };
    }

    public async Task<List<ProductSalesResponse>> GetTopSellingProductsAsync(
        ClaimsPrincipal user,
        int? requestedBranchId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var branchId = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        DateTime? startUtc = fromDate?.Date;
        DateTime? endExclusiveUtc = toDate?.Date.AddDays(1);

        var query = _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order != null && oi.Order.Status == OrderStatus.Paid && oi.Order.PaidAt != null)
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(oi => oi.Order!.BranchId == branchId.Value);
        }

        if (startUtc.HasValue)
        {
            query = query.Where(oi => oi.Order!.PaidAt >= startUtc.Value);
        }

        if (endExclusiveUtc.HasValue)
        {
            query = query.Where(oi => oi.Order!.PaidAt < endExclusiveUtc.Value);
        }

        var result = await query
            .GroupBy(oi => new { oi.ProductNameSnapshot, oi.SizeSnapshot })
            .Select(g => new ProductSalesResponse
            {
                ProductName = g.Key.ProductNameSnapshot,
                Size = g.Key.SizeSnapshot,
                QuantitySold = g.Sum(x => x.Quantity),
                TotalSalesAmount = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(r => r.QuantitySold)
            .ThenByDescending(r => r.TotalSalesAmount)
            .ToListAsync();

        return result;
    }
}
