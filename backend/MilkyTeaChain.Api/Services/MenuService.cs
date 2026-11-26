using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Menu;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class MenuService
{
    private readonly AppDbContext _context;
    private readonly BranchAccessService _branchAccessService;

    public MenuService(AppDbContext context, BranchAccessService branchAccessService)
    {
        _context = context;
        _branchAccessService = branchAccessService;
    }

    /// <summary>
    /// Lấy thực đơn đầy đủ kèm:
    /// - defaultPrice: Giá chung toàn chuỗi
    /// - branchOverridePrice: Giá riêng của chi nhánh (null nếu đang dùng giá chung)
    /// - effectivePrice / price: Giá thực tế đang áp dụng tại chi nhánh
    /// </summary>
    public async Task<object> GetFullMenuAsync(int? branchId = null)
    {
        var variantOverrides = branchId.HasValue
            ? await _context.BranchVariantPrices
                .Where(vp => vp.BranchId == branchId.Value)
                .ToDictionaryAsync(vp => vp.ProductVariantId, vp => vp.Price)
            : new Dictionary<int, decimal>();

        var toppingOverrides = branchId.HasValue
            ? await _context.BranchToppingPrices
                .Where(tp => tp.BranchId == branchId.Value)
                .ToDictionaryAsync(tp => tp.ToppingId, tp => tp.Price)
            : new Dictionary<int, decimal>();

        var categoriesRaw = await _context.Categories
            .Where(c => c.IsActive)
            .Include(c => c.Products.Where(p => p.IsActive))
                .ThenInclude(p => p.Variants.Where(v => v.IsActive))
            .OrderBy(c => c.Id)
            .ToListAsync();

        var categories = categoriesRaw.Select(c => new
        {
            c.Id,
            c.Name,
            Products = c.Products.Select(p => new
            {
                p.Id,
                p.Name,
                Variants = p.Variants.OrderBy(v => v.Id).Select(v =>
                {
                    bool hasOverride = variantOverrides.TryGetValue(v.Id, out var ovPrice);
                    decimal effective = hasOverride ? ovPrice : v.Price;
                    return new
                    {
                        v.Id,
                        v.Size,
                        DefaultPrice = v.Price,
                        BranchOverridePrice = hasOverride ? (decimal?)ovPrice : null,
                        EffectivePrice = effective,
                        Price = effective
                    };
                }).ToList()
            }).ToList()
        }).ToList();

        var toppingsRaw = await _context.Toppings
            .Where(t => t.IsActive)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var toppings = toppingsRaw.Select(t =>
        {
            bool hasOverride = toppingOverrides.TryGetValue(t.Id, out var ovPrice);
            decimal effective = hasOverride ? ovPrice : t.Price;
            return new
            {
                t.Id,
                t.Name,
                DefaultPrice = t.Price,
                BranchOverridePrice = hasOverride ? (decimal?)ovPrice : null,
                EffectivePrice = effective,
                Price = effective
            };
        }).ToList();

        return new
        {
            BranchId = branchId,
            Categories = categories,
            Toppings = toppings
        };
    }

    public async Task<Category> CreateCategoryAsync(SaveCategoryRequest request)
    {
        var name = request.Name.Trim();
        if (await _context.Categories.AnyAsync(c => c.Name == name))
        {
            throw new InvalidOperationException($"Danh mục '{name}' đã tồn tại.");
        }

        var category = new Category
        {
            Name = name,
            IsActive = true
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Product> CreateProductAsync(SaveProductRequest request)
    {
        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive);
        if (!categoryExists)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục mã {request.CategoryId}.");
        }

        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<ProductVariant> SaveVariantAsync(ClaimsPrincipal user, SaveVariantRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive)
            ?? throw new KeyNotFoundException($"Không tìm thấy món mã {request.ProductId}.");

        var size = request.Size.Trim().ToUpperInvariant();
        var existing = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.ProductId == request.ProductId && v.Size == size);

        var (actorId, actorName) = GetActorInfo(user);

        if (existing != null)
        {
            var oldPrice = existing.Price;
            existing.Price = request.Price;
            existing.IsActive = true;

            if (oldPrice != request.Price)
            {
                _context.PriceChangeHistories.Add(new PriceChangeHistory
                {
                    ItemType = "Variant",
                    ItemId = existing.Id,
                    ItemName = $"{product.Name} (Size {existing.Size})",
                    BranchId = null,
                    ScopeName = "Toàn chuỗi (Giá chung)",
                    OldPrice = oldPrice,
                    NewPrice = request.Price,
                    ActionType = "SetDefault",
                    ChangedByUserId = actorId,
                    ChangedByName = actorName,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        var variant = new ProductVariant
        {
            ProductId = request.ProductId,
            Size = size,
            Price = request.Price,
            IsActive = true
        };

        _context.ProductVariants.Add(variant);
        await _context.SaveChangesAsync();

        _context.PriceChangeHistories.Add(new PriceChangeHistory
        {
            ItemType = "Variant",
            ItemId = variant.Id,
            ItemName = $"{product.Name} (Size {variant.Size})",
            BranchId = null,
            ScopeName = "Toàn chuỗi (Giá chung)",
            OldPrice = 0m,
            NewPrice = request.Price,
            ActionType = "SetDefault",
            ChangedByUserId = actorId,
            ChangedByName = actorName,
            ChangedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return variant;
    }

    public async Task<Topping> SaveToppingAsync(ClaimsPrincipal user, SaveToppingRequest request)
    {
        var name = request.Name.Trim();
        var existing = await _context.Toppings.FirstOrDefaultAsync(t => t.Name == name);
        var (actorId, actorName) = GetActorInfo(user);

        if (existing != null)
        {
            var oldPrice = existing.Price;
            existing.Price = request.Price;
            existing.IsActive = true;

            if (oldPrice != request.Price)
            {
                _context.PriceChangeHistories.Add(new PriceChangeHistory
                {
                    ItemType = "Topping",
                    ItemId = existing.Id,
                    ItemName = $"Topping: {existing.Name}",
                    BranchId = null,
                    ScopeName = "Toàn chuỗi (Giá chung)",
                    OldPrice = oldPrice,
                    NewPrice = request.Price,
                    ActionType = "SetDefault",
                    ChangedByUserId = actorId,
                    ChangedByName = actorName,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        var topping = new Topping
        {
            Name = name,
            Price = request.Price,
            IsActive = true
        };

        _context.Toppings.Add(topping);
        await _context.SaveChangesAsync();

        _context.PriceChangeHistories.Add(new PriceChangeHistory
        {
            ItemType = "Topping",
            ItemId = topping.Id,
            ItemName = $"Topping: {topping.Name}",
            BranchId = null,
            ScopeName = "Toàn chuỗi (Giá chung)",
            OldPrice = 0m,
            NewPrice = request.Price,
            ActionType = "SetDefault",
            ChangedByUserId = actorId,
            ChangedByName = actorName,
            ChangedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return topping;
    }

    /// <summary>
    /// Đặt giá riêng hoặc chọn "Dùng lại giá chung" cho biến thể món tại một chi nhánh.
    /// Admin được sửa mọi chi nhánh; Manager chỉ được sửa tại chi nhánh mình phụ trách.
    /// </summary>
    public async Task<object> SetBranchVariantPriceAsync(
        ClaimsPrincipal user,
        int branchId,
        int variantId,
        SetBranchPriceRequest request)
    {
        _branchAccessService.EnsureCanAccessBranch(user, branchId);

        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {branchId}.");

        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId)
            ?? throw new KeyNotFoundException($"Không tìm thấy biến thể món mã {variantId}.");

        var existingOverride = await _context.BranchVariantPrices
            .FirstOrDefaultAsync(vp => vp.BranchId == branchId && vp.ProductVariantId == variantId);

        decimal oldEffectivePrice = existingOverride?.Price ?? variant.Price;
        var (actorId, actorName) = GetActorInfo(user);
        string itemName = $"{variant.Product?.Name} (Size {variant.Size})";

        if (request.UseDefaultPrice || !request.Price.HasValue)
        {
            if (existingOverride != null)
            {
                _context.BranchVariantPrices.Remove(existingOverride);
            }

            if (oldEffectivePrice != variant.Price)
            {
                _context.PriceChangeHistories.Add(new PriceChangeHistory
                {
                    ItemType = "Variant",
                    ItemId = variant.Id,
                    ItemName = itemName,
                    BranchId = branch.Id,
                    ScopeName = $"{branch.Name} (Dùng lại giá chung)",
                    OldPrice = oldEffectivePrice,
                    NewPrice = variant.Price,
                    ActionType = "ResetToDefault",
                    ChangedByUserId = actorId,
                    ChangedByName = actorName,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return new
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                VariantId = variant.Id,
                ItemName = itemName,
                DefaultPrice = variant.Price,
                BranchOverridePrice = (decimal?)null,
                EffectivePrice = variant.Price
            };
        }

        if (request.Price.Value <= 0)
        {
            throw new ArgumentException("Giá bán phải lớn hơn 0.");
        }

        if (existingOverride == null)
        {
            existingOverride = new BranchVariantPrice
            {
                BranchId = branch.Id,
                ProductVariantId = variant.Id,
                Price = request.Price.Value,
                UpdatedAt = DateTime.UtcNow
            };
            _context.BranchVariantPrices.Add(existingOverride);
        }
        else
        {
            existingOverride.Price = request.Price.Value;
            existingOverride.UpdatedAt = DateTime.UtcNow;
        }

        if (oldEffectivePrice != request.Price.Value)
        {
            _context.PriceChangeHistories.Add(new PriceChangeHistory
            {
                ItemType = "Variant",
                ItemId = variant.Id,
                ItemName = itemName,
                BranchId = branch.Id,
                ScopeName = $"{branch.Name} (Giá riêng)",
                OldPrice = oldEffectivePrice,
                NewPrice = request.Price.Value,
                ActionType = "SetBranchOverride",
                ChangedByUserId = actorId,
                ChangedByName = actorName,
                ChangedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return new
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            VariantId = variant.Id,
            ItemName = itemName,
            DefaultPrice = variant.Price,
            BranchOverridePrice = (decimal?)request.Price.Value,
            EffectivePrice = request.Price.Value
        };
    }

    /// <summary>
    /// Đặt giá riêng hoặc chọn "Dùng lại giá chung" cho Topping tại một chi nhánh.
    /// </summary>
    public async Task<object> SetBranchToppingPriceAsync(
        ClaimsPrincipal user,
        int branchId,
        int toppingId,
        SetBranchPriceRequest request)
    {
        _branchAccessService.EnsureCanAccessBranch(user, branchId);

        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {branchId}.");

        var topping = await _context.Toppings.FindAsync(toppingId)
            ?? throw new KeyNotFoundException($"Không tìm thấy topping mã {toppingId}.");

        var existingOverride = await _context.BranchToppingPrices
            .FirstOrDefaultAsync(tp => tp.BranchId == branchId && tp.ToppingId == toppingId);

        decimal oldEffectivePrice = existingOverride?.Price ?? topping.Price;
        var (actorId, actorName) = GetActorInfo(user);
        string itemName = $"Topping: {topping.Name}";

        if (request.UseDefaultPrice || !request.Price.HasValue)
        {
            if (existingOverride != null)
            {
                _context.BranchToppingPrices.Remove(existingOverride);
            }

            if (oldEffectivePrice != topping.Price)
            {
                _context.PriceChangeHistories.Add(new PriceChangeHistory
                {
                    ItemType = "Topping",
                    ItemId = topping.Id,
                    ItemName = itemName,
                    BranchId = branch.Id,
                    ScopeName = $"{branch.Name} (Dùng lại giá chung)",
                    OldPrice = oldEffectivePrice,
                    NewPrice = topping.Price,
                    ActionType = "ResetToDefault",
                    ChangedByUserId = actorId,
                    ChangedByName = actorName,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return new
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                ToppingId = topping.Id,
                ItemName = itemName,
                DefaultPrice = topping.Price,
                BranchOverridePrice = (decimal?)null,
                EffectivePrice = topping.Price
            };
        }

        if (request.Price.Value < 0)
        {
            throw new ArgumentException("Giá topping không được âm.");
        }

        if (existingOverride == null)
        {
            existingOverride = new BranchToppingPrice
            {
                BranchId = branch.Id,
                ToppingId = topping.Id,
                Price = request.Price.Value,
                UpdatedAt = DateTime.UtcNow
            };
            _context.BranchToppingPrices.Add(existingOverride);
        }
        else
        {
            existingOverride.Price = request.Price.Value;
            existingOverride.UpdatedAt = DateTime.UtcNow;
        }

        if (oldEffectivePrice != request.Price.Value)
        {
            _context.PriceChangeHistories.Add(new PriceChangeHistory
            {
                ItemType = "Topping",
                ItemId = topping.Id,
                ItemName = itemName,
                BranchId = branch.Id,
                ScopeName = $"{branch.Name} (Giá riêng)",
                OldPrice = oldEffectivePrice,
                NewPrice = request.Price.Value,
                ActionType = "SetBranchOverride",
                ChangedByUserId = actorId,
                ChangedByName = actorName,
                ChangedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return new
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            ToppingId = topping.Id,
            ItemName = itemName,
            DefaultPrice = topping.Price,
            BranchOverridePrice = (decimal?)request.Price.Value,
            EffectivePrice = request.Price.Value
        };
    }

    public async Task<List<PriceChangeHistory>> GetPriceHistoryAsync(ClaimsPrincipal user, int? requestedBranchId)
    {
        var branchScope = _branchAccessService.ResolveReportBranchScope(user, requestedBranchId);

        var query = _context.PriceChangeHistories.AsQueryable();
        if (branchScope.HasValue)
        {
            // Manager xem lịch sử đổi giá của chi nhánh mình + lịch sử giá chung toàn chuỗi
            query = query.Where(h => h.BranchId == branchScope.Value || h.BranchId == null);
        }

        return await query
            .OrderByDescending(h => h.ChangedAt)
            .Take(100)
            .ToListAsync();
    }

    private (int UserId, string FullName) GetActorInfo(ClaimsPrincipal user)
    {
        var userId = _branchAccessService.GetCurrentUserId(user);
        var fullName = user.FindFirst("FullName")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? $"User #{userId}";
        return (userId, fullName);
    }
}
