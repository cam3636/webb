using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Orders;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class OrderService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public OrderService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    /// <summary>
    /// Tạo đơn hàng:
    /// - Tính tiền theo giá thực tế của chi nhánh bán (ưu tiên BranchVariantPrice / BranchToppingPrice nếu có, ngược lại dùng giá chung).
    /// - Nếu giá đã đổi trong lúc thu ngân đang mở giỏ (ExpectedUnitPrice / ExpectedToppingTotal khác giá hiện tại) và chưa xác nhận (ConfirmPriceChange = false),
    ///   ném ngoại lệ thông báo rõ giá mới để thu ngân xác nhận lại trước khi thu tiền.
    /// - Lưu đơn giá món và topping vào từng dòng hóa đơn tại thời điểm bán.
    /// </summary>
    public async Task<OrderResponse> CreateOrderAsync(ClaimsPrincipal user, CreateOrderRequest request)
    {
        var branchId = _branchAccessService.ResolveAccessibleBranchId(user, request.BranchId);
        var cashierId = _branchAccessService.GetCurrentUserId(user);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive)
            ?? throw new KeyNotFoundException($"Chi nhánh mã {branchId} không tồn tại hoặc đã ngừng hoạt động.");

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Đơn hàng phải có ít nhất một dòng món.");
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _context.Customers.FindAsync(request.CustomerId.Value)
                ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng mã {request.CustomerId.Value}.");
        }

        var resolvedVariantIds = request.Items
            .Select(i => i.VariantId > 0 ? i.VariantId : i.ProductVariantId)
            .Distinct()
            .ToList();

        if (resolvedVariantIds.Any(id => id <= 0))
        {
            throw new ArgumentException("Mã biến thể (variantId) không hợp lệ.");
        }

        var variants = await _context.ProductVariants
            .Include(v => v.Product)
            .Where(v => resolvedVariantIds.Contains(v.Id) && v.IsActive && v.Product != null && v.Product.IsActive)
            .ToDictionaryAsync(v => v.Id);

        if (variants.Count != resolvedVariantIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều biến thể món không tồn tại hoặc đã ngừng bán.");
        }

        // Lấy bảng giá riêng của chi nhánh bán (nếu có)
        var branchVariantOverrides = await _context.BranchVariantPrices
            .Where(vp => vp.BranchId == branchId && resolvedVariantIds.Contains(vp.ProductVariantId))
            .ToDictionaryAsync(vp => vp.ProductVariantId, vp => vp.Price);

        var allToppingIds = request.Items
            .SelectMany(i => i.ToppingIds ?? new List<int>())
            .Distinct()
            .ToList();

        var toppings = allToppingIds.Count > 0
            ? await _context.Toppings
                .Where(t => allToppingIds.Contains(t.Id) && t.IsActive)
                .ToDictionaryAsync(t => t.Id)
            : new Dictionary<int, Topping>();

        if (toppings.Count != allToppingIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều topping không tồn tại hoặc đã ngừng bán.");
        }

        var branchToppingOverrides = allToppingIds.Count > 0
            ? await _context.BranchToppingPrices
                .Where(tp => tp.BranchId == branchId && allToppingIds.Contains(tp.ToppingId))
                .ToDictionaryAsync(tp => tp.ToppingId, tp => tp.Price)
            : new Dictionary<int, decimal>();

        var priceChangesDetected = new List<string>();

        var order = new Order
        {
            BranchId = branchId,
            CashierId = cashierId,
            CustomerId = customer?.Id,
            PointsEarned = 0,
            Status = OrderStatus.Pending,
            PreparationStage = "Waiting",
            IsPrepared = false,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = 0m
        };

        decimal orderTotal = 0m;

        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0)
            {
                throw new ArgumentException("Số lượng mỗi dòng món phải lớn hơn 0.");
            }

            var resolvedVarId = itemReq.VariantId > 0 ? itemReq.VariantId : itemReq.ProductVariantId;
            var variant = variants[resolvedVarId];

            decimal effectiveVariantPrice = branchVariantOverrides.TryGetValue(variant.Id, out var ovVarPrice)
                ? ovVarPrice
                : variant.Price;

            if (!request.ConfirmPriceChange && itemReq.ExpectedUnitPrice.HasValue && itemReq.ExpectedUnitPrice.Value != effectiveVariantPrice)
            {
                priceChangesDetected.Add(
                    $"{variant.Product!.Name} (Size {variant.Size}): giá trong giỏ {itemReq.ExpectedUnitPrice.Value:N0} đ -> giá mới tại chi nhánh {effectiveVariantPrice:N0} đ");
            }

            var orderItem = new OrderItem
            {
                ProductVariantId = variant.Id,
                ProductNameSnapshot = variant.Product!.Name,
                SizeSnapshot = variant.Size,
                Note = itemReq.Note?.Trim() ?? string.Empty,
                Quantity = itemReq.Quantity,
                UnitPrice = effectiveVariantPrice
            };

            decimal toppingSumPerCup = 0m;
            if (itemReq.ToppingIds != null)
            {
                foreach (var topId in itemReq.ToppingIds)
                {
                    var top = toppings[topId];
                    decimal effectiveTopPrice = branchToppingOverrides.TryGetValue(top.Id, out var ovTopPrice)
                        ? ovTopPrice
                        : top.Price;

                    toppingSumPerCup += effectiveTopPrice;
                    orderItem.Toppings.Add(new OrderItemTopping
                    {
                        ToppingId = top.Id,
                        ToppingNameSnapshot = top.Name,
                        Price = effectiveTopPrice
                    });
                }
            }

            if (!request.ConfirmPriceChange && itemReq.ExpectedToppingTotal.HasValue && itemReq.ExpectedToppingTotal.Value != toppingSumPerCup)
            {
                priceChangesDetected.Add(
                    $"Topping của {variant.Product!.Name} (Size {variant.Size}): giá trong giỏ {itemReq.ExpectedToppingTotal.Value:N0} đ -> giá mới {toppingSumPerCup:N0} đ");
            }

            orderItem.LineTotal = (orderItem.UnitPrice + toppingSumPerCup) * orderItem.Quantity;
            orderTotal += orderItem.LineTotal;

            order.Items.Add(orderItem);
        }

        if (priceChangesDetected.Count > 0)
        {
            throw new InvalidOperationException(
                $"PRICE_CHANGED: Bảng giá vừa được cập nhật trong lúc bạn đang mở giỏ hàng ({string.Join("; ", priceChangesDetected)}). Vui lòng xác nhận giá mới trước khi thu tiền.");
        }

        order.TotalAmount = orderTotal;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return await GetOrderByIdAsync(user, order.Id);
    }

    public async Task<OrderResponse> GetOrderByIdAsync(ClaimsPrincipal user, int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng mã {orderId}.");

        var role = _branchAccessService.GetCurrentUserRole(user);

        _branchAccessService.EnsureCanAccessBranch(user, order.BranchId);

        if (role == UserRole.Barista && order.Status != OrderStatus.Paid)
        {
            throw new UnauthorizedAccessException("Barista chỉ được phép xem các đơn hàng đã thanh toán (Paid).");
        }

        return MapToOrderResponse(order);
    }

    public async Task<List<OrderResponse>> GetOrdersAsync(ClaimsPrincipal user, int? requestedBranchId)
    {
        var role = _branchAccessService.GetCurrentUserRole(user);
        var branchScope = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        var query = _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .AsQueryable();

        if (branchScope.HasValue)
        {
            query = query.Where(o => o.BranchId == branchScope.Value);
        }

        if (role == UserRole.Barista)
        {
            query = query.Where(o => o.Status == OrderStatus.Paid);
        }

        var orders = await query
            .OrderByDescending(o => o.PaidAt ?? o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToOrderResponse).ToList();
    }

    public async Task<List<OrderResponse>> GetBaristaPaidOrdersAsync(ClaimsPrincipal user, int? requestedBranchId)
    {
        var branchId = _branchAccessService.ResolveAccessibleBranchId(user, requestedBranchId);

        var orders = await _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Paid)
            .OrderBy(o => o.IsPrepared)
            .ThenByDescending(o => o.PaidAt ?? o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToOrderResponse).ToList();
    }

    public async Task<OrderResponse> StartPreparingOrderAsync(ClaimsPrincipal user, int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng mã {orderId}.");

        _branchAccessService.EnsureCanAccessBranch(user, order.BranchId);

        if (order.Status != OrderStatus.Paid)
        {
            throw new InvalidOperationException("Chỉ có thể bắt đầu pha chế đơn hàng đã thanh toán (Paid).");
        }

        if (!order.IsPrepared && order.PreparationStage != "Preparing")
        {
            order.PreparationStage = "Preparing";
            await _context.SaveChangesAsync();
        }

        return MapToOrderResponse(order);
    }

    public async Task<OrderResponse> MarkOrderPreparedAsync(ClaimsPrincipal user, int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng mã {orderId}.");

        _branchAccessService.EnsureCanAccessBranch(user, order.BranchId);

        if (order.Status != OrderStatus.Paid)
        {
            throw new InvalidOperationException("Chỉ có thể đánh dấu 'Hoàn thành pha chế' cho đơn hàng đã thanh toán (Paid).");
        }

        // Hoàn thành pha chế chỉ đổi PreparationStage/IsPrepared, tuyệt đối không cộng thêm điểm hay doanh thu
        if (!order.IsPrepared || order.PreparationStage != "Completed")
        {
            order.IsPrepared = true;
            order.PreparationStage = "Completed";
            order.PreparedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return MapToOrderResponse(order);
    }

    public static OrderResponse MapToOrderResponse(Order order)
    {
        string stage = order.IsPrepared
            ? "Completed"
            : (string.IsNullOrWhiteSpace(order.PreparationStage) ? "Waiting" : order.PreparationStage);

        return new OrderResponse
        {
            OrderId = order.Id,
            Id = order.Id,
            BranchId = order.BranchId,
            BranchName = order.Branch?.Name ?? string.Empty,
            BranchCity = order.Branch?.City ?? string.Empty,
            BranchAddress = order.Branch?.Address ?? string.Empty,
            BranchPhone = order.Branch?.Phone ?? string.Empty,
            CashierId = order.CashierId,
            CashierName = order.Cashier?.FullName ?? string.Empty,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.FullName,
            CustomerPhone = order.Customer?.Phone,
            PointsEarned = order.PointsEarned,
            CustomerTotalPoints = order.Customer?.Points,
            Status = order.Status.ToString(),
            IsPrepared = order.IsPrepared,
            PreparationStatus = stage,
            PreparedAt = order.PreparedAt,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            PaidAt = order.PaidAt,
            Items = order.Items.Select(i => new OrderItemResponse
            {
                Id = i.Id,
                VariantId = i.ProductVariantId,
                ProductVariantId = i.ProductVariantId,
                ProductName = i.ProductNameSnapshot,
                Size = i.SizeSnapshot,
                Note = i.Note,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal,
                Toppings = i.Toppings.Select(t => $"{t.ToppingNameSnapshot} (+{t.Price:N0}đ)").ToList()
            }).ToList()
        };
    }
}
