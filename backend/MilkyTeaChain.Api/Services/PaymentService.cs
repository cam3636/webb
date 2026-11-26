using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Orders;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class PaymentService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public PaymentService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    /// <summary>
    /// Xác nhận thanh toán, trừ kho theo công thức và tích điểm khách hàng thân thiết trong cùng một giao dịch:
    /// - Mỗi 10.000 đồng thực trả được 1 điểm, làm tròn xuống theo từng hóa đơn: (int)Math.Floor(order.TotalAmount / 10000m).
    /// - Nếu đơn đã Paid trước đó (bấm thanh toán lại), trả về ngay kết quả, không trừ kho lần hai và không cộng thêm điểm.
    /// </summary>
    public async Task<OrderResponse> PayOrderAsync(ClaimsPrincipal user, int orderId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var order = await _context.Orders
            .Include(o => o.Branch)
            .Include(o => o.Cashier)
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng mã {orderId}.");

        _branchAccessService.EnsureCanAccessBranch(user, order.BranchId);

        // Nếu đơn đã Paid trước đó -> trả về ngay kết quả (Idempotent), không trừ kho hay cộng điểm lần thứ hai
        if (order.Status == OrderStatus.Paid)
        {
            await transaction.CommitAsync();
            return OrderService.MapToOrderResponse(order);
        }

        var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variantRecipes = await _context.VariantRecipeItems
            .Where(r => variantIds.Contains(r.ProductVariantId))
            .ToListAsync();

        var toppingIds = order.Items
            .SelectMany(i => i.Toppings.Select(t => t.ToppingId))
            .Distinct()
            .ToList();

        var toppingRecipes = toppingIds.Count > 0
            ? await _context.ToppingRecipeItems
                .Where(r => toppingIds.Contains(r.ToppingId))
                .ToListAsync()
            : new List<ToppingRecipeItem>();

        var requiredIngredients = new Dictionary<int, decimal>();

        foreach (var item in order.Items)
        {
            var vRecipes = variantRecipes.Where(r => r.ProductVariantId == item.ProductVariantId);
            foreach (var vr in vRecipes)
            {
                var amount = vr.Quantity * item.Quantity;
                requiredIngredients[vr.IngredientId] = requiredIngredients.GetValueOrDefault(vr.IngredientId) + amount;
            }

            foreach (var itemTopping in item.Toppings)
            {
                var tRecipes = toppingRecipes.Where(r => r.ToppingId == itemTopping.ToppingId);
                foreach (var tr in tRecipes)
                {
                    var amount = tr.Quantity * item.Quantity;
                    requiredIngredients[tr.IngredientId] = requiredIngredients.GetValueOrDefault(tr.IngredientId) + amount;
                }
            }
        }

        if (requiredIngredients.Count > 0)
        {
            var reqIngredientIds = requiredIngredients.Keys.ToList();

            var inventories = await _context.BranchInventories
                .Include(bi => bi.Ingredient)
                .Where(bi => bi.BranchId == order.BranchId && reqIngredientIds.Contains(bi.IngredientId))
                .ToDictionaryAsync(bi => bi.IngredientId);

            var ingredientNames = await _context.Ingredients
                .Where(i => reqIngredientIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => $"{i.Name} ({i.Unit})");

            var shortages = new List<string>();
            foreach (var (ingredientId, neededQty) in requiredIngredients)
            {
                var availableQty = inventories.TryGetValue(ingredientId, out var inv) ? inv.Quantity : 0m;
                if (availableQty < neededQty)
                {
                    var name = ingredientNames.GetValueOrDefault(ingredientId, $"Mã NL {ingredientId}");
                    shortages.Add($"{name}: cần {neededQty:N2}, tồn {availableQty:N2}");
                }
            }

            if (shortages.Count > 0)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    $"Kho chi nhánh không đủ nguyên liệu để thanh toán đơn #{order.Id}. Chi tiết thiếu: {string.Join("; ", shortages)}");
            }

            foreach (var (ingredientId, neededQty) in requiredIngredients)
            {
                var inv = inventories[ingredientId];
                inv.Quantity -= neededQty;
            }
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;

        // Tích điểm khách hàng thân thiết: mỗi 10.000 đồng thực trả được 1 điểm, làm tròn xuống theo từng hóa đơn
        if (order.CustomerId.HasValue && order.Customer != null)
        {
            bool alreadyCredited = await _context.CustomerPointTransactions
                .AnyAsync(pt => pt.OrderId == order.Id);

            if (!alreadyCredited)
            {
                int earned = (int)Math.Floor(order.TotalAmount / 10000m);
                order.PointsEarned = earned;
                order.Customer.Points += earned;

                _context.CustomerPointTransactions.Add(new CustomerPointTransaction
                {
                    CustomerId = order.Customer.Id,
                    OrderId = order.Id,
                    BranchId = order.BranchId,
                    PointsEarned = earned,
                    OrderAmount = order.TotalAmount,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Xung đột dữ liệu tồn kho khi hai giao dịch thanh toán đồng thời. Vui lòng thử lại.");
        }

        return OrderService.MapToOrderResponse(order);
    }
}
