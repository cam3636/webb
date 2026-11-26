using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Customers;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class CustomerService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public CustomerService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    /// <summary>
    /// Chuẩn hóa số điện thoại dùng chung toàn chuỗi:
    /// - Bỏ khoảng trắng, dấu gạch ngang, dấu chấm, dấu ngoặc.
    /// - Đổi đầu +84 hoặc 84 thành 0.
    /// </summary>
    public static string NormalizePhone(string rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(rawPhone.Trim(), @"[\s\-\.\(\)]", "");
        if (cleaned.StartsWith("+84"))
        {
            cleaned = "0" + cleaned[3..];
        }
        else if (cleaned.StartsWith("84") && cleaned.Length >= 10)
        {
            cleaned = "0" + cleaned[2..];
        }

        return cleaned;
    }

    public async Task<CustomerResponse?> LookupByPhoneAsync(string phone)
    {
        var normalized = NormalizePhone(phone);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Số điện thoại không hợp lệ.");
        }

        var customer = await _context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Phone == normalized);

        if (customer == null)
        {
            return null;
        }

        var paidOrders = customer.Orders.Where(o => o.Status == OrderStatus.Paid).ToList();

        return new CustomerResponse
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Phone = customer.Phone,
            Points = customer.Points,
            PurchaseCount = paidOrders.Count,
            LastPurchaseAt = paidOrders.OrderByDescending(o => o.PaidAt).Select(o => o.PaidAt).FirstOrDefault(),
            TotalSpent = paidOrders.Sum(o => o.TotalAmount)
        };
    }

    public async Task<CustomerResponse> CreateOrGetCustomerAsync(SaveCustomerRequest request)
    {
        var normalized = NormalizePhone(request.Phone);
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 8)
        {
            throw new ArgumentException("Số điện thoại không hợp lệ (cần ít nhất 8 chữ số).");
        }

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(fullName))
        {
            throw new ArgumentException("Vui lòng nhập họ tên khách hàng.");
        }

        var existing = await _context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Phone == normalized);

        if (existing != null)
        {
            throw new InvalidOperationException($"Số điện thoại '{normalized}' đã được đăng ký cho khách hàng '{existing.FullName}' ({existing.Points} điểm).");
        }

        var customer = new Customer
        {
            FullName = fullName,
            Phone = normalized,
            Points = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return new CustomerResponse
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Phone = customer.Phone,
            Points = customer.Points,
            PurchaseCount = 0,
            LastPurchaseAt = null,
            TotalSpent = 0m
        };
    }

    /// <summary>
    /// Danh sách khách hàng:
    /// - Admin: xem toàn chuỗi (số lần mua và lần mua gần nhất trên toàn chuỗi hoặc lọc theo chi nhánh).
    /// - Manager: xem khách hàng và thống kê giao dịch tại chi nhánh của mình.
    /// </summary>
    public async Task<List<CustomerResponse>> GetCustomersListAsync(ClaimsPrincipal user, int? requestedBranchId, string? search)
    {
        var branchScope = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        var query = _context.Customers
            .Include(c => c.Orders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            var normPhone = NormalizePhone(kw);
            query = query.Where(c => c.FullName.Contains(kw) || c.Phone.Contains(normPhone));
        }

        var customers = await query
            .OrderByDescending(c => c.Points)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var result = new List<CustomerResponse>();
        foreach (var c in customers)
        {
            var paidOrders = c.Orders.Where(o => o.Status == OrderStatus.Paid);
            if (branchScope.HasValue)
            {
                paidOrders = paidOrders.Where(o => o.BranchId == branchScope.Value);
            }

            var paidList = paidOrders.ToList();

            result.Add(new CustomerResponse
            {
                Id = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                Points = c.Points, // Điểm tích lũy dùng chung toàn chuỗi
                PurchaseCount = paidList.Count,
                LastPurchaseAt = paidList.OrderByDescending(o => o.PaidAt).Select(o => o.PaidAt).FirstOrDefault(),
                TotalSpent = paidList.Sum(o => o.TotalAmount)
            });
        }

        return result;
    }

    /// <summary>
    /// Xem lịch sử giao dịch & tích điểm của khách hàng:
    /// - Admin xem toàn chuỗi.
    /// - Manager chỉ xem giao dịch phát sinh tại chi nhánh của mình.
    /// </summary>
    public async Task<object> GetCustomerTransactionsAsync(ClaimsPrincipal user, int customerId, int? requestedBranchId)
    {
        var branchScope = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        var customer = await _context.Customers.FindAsync(customerId)
            ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng mã {customerId}.");

        var txQuery = _context.CustomerPointTransactions
            .Include(t => t.Branch)
            .Include(t => t.Order)
            .Where(t => t.CustomerId == customerId)
            .AsQueryable();

        if (branchScope.HasValue)
        {
            txQuery = txQuery.Where(t => t.BranchId == branchScope.Value);
        }

        var transactions = await txQuery
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.OrderId,
                t.BranchId,
                BranchName = t.Branch != null ? t.Branch.Name : string.Empty,
                t.OrderAmount,
                t.PointsEarned,
                t.CreatedAt
            })
            .ToListAsync();

        return new
        {
            Customer = new
            {
                customer.Id,
                customer.FullName,
                customer.Phone,
                customer.Points
            },
            BranchScope = branchScope,
            Transactions = transactions
        };
    }
}
