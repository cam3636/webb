using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Inventory;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class InventoryService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public InventoryService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    public async Task<List<Ingredient>> GetAllIngredientsAsync()
    {
        return await _context.Ingredients
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    public async Task<Ingredient> SaveIngredientAsync(SaveIngredientRequest request)
    {
        var name = request.Name.Trim();
        var unit = request.Unit.Trim().ToLowerInvariant();
        if (unit != "g" && unit != "ml")
        {
            throw new ArgumentException("Đơn vị tính của nguyên liệu chỉ chấp nhận 'g' hoặc 'ml'.");
        }

        var existing = await _context.Ingredients.FirstOrDefaultAsync(i => i.Name == name);
        if (existing != null)
        {
            existing.Unit = unit;
            existing.IsActive = true;
            await _context.SaveChangesAsync();
            return existing;
        }

        var ingredient = new Ingredient
        {
            Name = name,
            Unit = unit,
            IsActive = true
        };

        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<object> GetBranchInventoryAsync(ClaimsPrincipal user, int? requestedBranchId)
    {
        var branchId = _branchAccessService.ResolveAccessibleBranchId(user, requestedBranchId);

        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {branchId}.");

        var items = await _context.BranchInventories
            .Where(bi => bi.BranchId == branchId)
            .Include(bi => bi.Ingredient)
            .OrderBy(bi => bi.IngredientId)
            .Select(bi => new
            {
                bi.IngredientId,
                IngredientName = bi.Ingredient != null ? bi.Ingredient.Name : string.Empty,
                Unit = bi.Ingredient != null ? bi.Ingredient.Unit : string.Empty,
                bi.Quantity
            })
            .ToListAsync();

        return new
        {
            BranchId = branch.Id,
            BranchCode = branch.Code,
            BranchName = branch.Name,
            Inventories = items
        };
    }

    public async Task<object> CreateStockReceiptAsync(ClaimsPrincipal user, CreateStockReceiptRequest request)
    {
        var branchId = _branchAccessService.ResolveAccessibleBranchId(user, request.BranchId);
        var userId = _branchAccessService.GetCurrentUserId(user);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive)
            ?? throw new KeyNotFoundException($"Chi nhánh mã {branchId} không tồn tại hoặc đã ngừng hoạt động.");

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Phiếu nhập kho phải có ít nhất một nguyên liệu.");
        }

        var ingredientIds = request.Items.Select(i => i.IngredientId).Distinct().ToList();
        var validIngredients = await _context.Ingredients
            .Where(i => ingredientIds.Contains(i.Id) && i.IsActive)
            .ToDictionaryAsync(i => i.Id);

        if (validIngredients.Count != ingredientIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều nguyên liệu trong phiếu nhập không tồn tại.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var receipt = new StockReceipt
        {
            BranchId = branchId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Note = request.Note?.Trim() ?? string.Empty
        };

        var groupedItems = request.Items
            .GroupBy(i => i.IngredientId)
            .Select(g => new
            {
                IngredientId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var item in groupedItems)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentException("Số lượng nhập kho phải lớn hơn 0.");
            }

            receipt.Items.Add(new StockReceiptItem
            {
                IngredientId = item.IngredientId,
                Quantity = item.Quantity
            });

            var inventory = await _context.BranchInventories
                .FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == item.IngredientId);

            if (inventory == null)
            {
                inventory = new BranchInventory
                {
                    BranchId = branchId,
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity
                };
                _context.BranchInventories.Add(inventory);
            }
            else
            {
                inventory.Quantity += item.Quantity;
            }
        }

        _context.StockReceipts.Add(receipt);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new
        {
            ReceiptId = receipt.Id,
            receipt.BranchId,
            BranchName = branch.Name,
            receipt.CreatedByUserId,
            receipt.CreatedAt,
            receipt.Note,
            Items = receipt.Items.Select(i => new
            {
                i.IngredientId,
                IngredientName = validIngredients[i.IngredientId].Name,
                Unit = validIngredients[i.IngredientId].Unit,
                i.Quantity
            })
        };
    }

    /// <summary>
    /// Tra cứu lịch sử Nhập kho (từ StockReceipts) và Xuất kho bán hàng (từ các đơn Paid theo công thức) của chi nhánh.
    /// </summary>
    public async Task<object> GetInventoryHistoryAsync(ClaimsPrincipal user, int? requestedBranchId)
    {
        var branchId = _branchAccessService.ResolveAccessibleBranchId(user, requestedBranchId);

        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {branchId}.");

        // 1. Lịch sử Nhập kho
        var receipts = await _context.StockReceipts
            .Where(r => r.BranchId == branchId)
            .Include(r => r.CreatedByUser)
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new
            {
                r.Id,
                Type = "IN",
                Title = $"Phiếu nhập kho #{r.Id}",
                Actor = r.CreatedByUser != null ? r.CreatedByUser.FullName : "Hệ thống",
                Timestamp = r.CreatedAt,
                r.Note,
                Details = r.Items.Select(i => new
                {
                    i.IngredientId,
                    IngredientName = i.Ingredient != null ? i.Ingredient.Name : string.Empty,
                    Unit = i.Ingredient != null ? i.Ingredient.Unit : string.Empty,
                    QuantityChange = i.Quantity
                }).ToList()
            })
            .ToListAsync();

        // 2. Lịch sử Xuất kho từ các đơn hàng đã thanh toán (Paid)
        var paidOrders = await _context.Orders
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Paid)
            .Include(o => o.Cashier)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .OrderByDescending(o => o.PaidAt)
            .Take(50)
            .ToListAsync();

        var variantRecipes = await _context.VariantRecipeItems
            .Include(r => r.Ingredient)
            .ToListAsync();

        var toppingRecipes = await _context.ToppingRecipeItems
            .Include(r => r.Ingredient)
            .ToListAsync();

        var stockOuts = new List<object>();

        foreach (var order in paidOrders)
        {
            var deductedMap = new Dictionary<int, (string Name, string Unit, decimal Qty)>();

            foreach (var item in order.Items)
            {
                foreach (var vr in variantRecipes.Where(r => r.ProductVariantId == item.ProductVariantId))
                {
                    var qty = vr.Quantity * item.Quantity;
                    if (!deductedMap.TryGetValue(vr.IngredientId, out var cur))
                    {
                        cur = (vr.Ingredient?.Name ?? "", vr.Ingredient?.Unit ?? "", 0m);
                    }
                    deductedMap[vr.IngredientId] = (cur.Name, cur.Unit, cur.Qty + qty);
                }

                foreach (var top in item.Toppings)
                {
                    foreach (var tr in toppingRecipes.Where(r => r.ToppingId == top.ToppingId))
                    {
                        var qty = tr.Quantity * item.Quantity;
                        if (!deductedMap.TryGetValue(tr.IngredientId, out var cur))
                        {
                            cur = (tr.Ingredient?.Name ?? "", tr.Ingredient?.Unit ?? "", 0m);
                        }
                        deductedMap[tr.IngredientId] = (cur.Name, cur.Unit, cur.Qty + qty);
                    }
                }
            }

            stockOuts.Add(new
            {
                Id = order.Id,
                Type = "OUT",
                Title = $"Xuất bán Đơn hàng #{order.Id}",
                Actor = order.Cashier?.FullName ?? "Thu ngân",
                Timestamp = order.PaidAt ?? order.CreatedAt,
                Note = $"Thanh toán đơn #{order.Id} ({order.TotalAmount:N0} đ)",
                Details = deductedMap.Select(kv => new
                {
                    IngredientId = kv.Key,
                    IngredientName = kv.Value.Name,
                    Unit = kv.Value.Unit,
                    QuantityChange = -kv.Value.Qty
                }).ToList()
            });
        }

        return new
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            Receipts = receipts,
            StockOuts = stockOuts
        };
    }
}
