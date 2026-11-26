using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Inventory;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class RecipeService
{
    private readonly AppDbContext _context;

    public RecipeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetVariantRecipeAsync(int variantId)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.RecipeItems)
                .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(v => v.Id == variantId)
            ?? throw new KeyNotFoundException($"Không tìm thấy biến thể món mã {variantId}.");

        return new
        {
            VariantId = variant.Id,
            ProductName = variant.Product?.Name,
            variant.Size,
            variant.Price,
            Ingredients = variant.RecipeItems.Select(r => new
            {
                r.IngredientId,
                IngredientName = r.Ingredient?.Name,
                Unit = r.Ingredient?.Unit,
                r.Quantity
            })
        };
    }

    public async Task<object> SetVariantRecipeAsync(int variantId, List<RecipeItemRequest> items)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.RecipeItems)
            .FirstOrDefaultAsync(v => v.Id == variantId)
            ?? throw new KeyNotFoundException($"Không tìm thấy biến thể món mã {variantId}.");

        var ingredientIds = items.Select(i => i.IngredientId).Distinct().ToList();
        var validCount = await _context.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id) && i.IsActive);
        if (validCount != ingredientIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều nguyên liệu trong công thức không tồn tại.");
        }

        _context.VariantRecipeItems.RemoveRange(variant.RecipeItems);

        var grouped = items
            .GroupBy(i => i.IngredientId)
            .Select(g => new VariantRecipeItem
            {
                ProductVariantId = variantId,
                IngredientId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        _context.VariantRecipeItems.AddRange(grouped);
        await _context.SaveChangesAsync();

        return await GetVariantRecipeAsync(variantId);
    }

    public async Task<object> GetToppingRecipeAsync(int toppingId)
    {
        var topping = await _context.Toppings
            .Include(t => t.RecipeItems)
                .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(t => t.Id == toppingId)
            ?? throw new KeyNotFoundException($"Không tìm thấy topping mã {toppingId}.");

        return new
        {
            ToppingId = topping.Id,
            topping.Name,
            topping.Price,
            Ingredients = topping.RecipeItems.Select(r => new
            {
                r.IngredientId,
                IngredientName = r.Ingredient?.Name,
                Unit = r.Ingredient?.Unit,
                r.Quantity
            })
        };
    }

    public async Task<object> SetToppingRecipeAsync(int toppingId, List<RecipeItemRequest> items)
    {
        var topping = await _context.Toppings
            .Include(t => t.RecipeItems)
            .FirstOrDefaultAsync(t => t.Id == toppingId)
            ?? throw new KeyNotFoundException($"Không tìm thấy topping mã {toppingId}.");

        var ingredientIds = items.Select(i => i.IngredientId).Distinct().ToList();
        var validCount = await _context.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id) && i.IsActive);
        if (validCount != ingredientIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều nguyên liệu trong công thức không tồn tại.");
        }

        _context.ToppingRecipeItems.RemoveRange(topping.RecipeItems);

        var grouped = items
            .GroupBy(i => i.IngredientId)
            .Select(g => new ToppingRecipeItem
            {
                ToppingId = toppingId,
                IngredientId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        _context.ToppingRecipeItems.AddRange(grouped);
        await _context.SaveChangesAsync();

        return await GetToppingRecipeAsync(toppingId);
    }
}
