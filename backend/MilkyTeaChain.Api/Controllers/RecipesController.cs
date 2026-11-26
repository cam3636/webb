using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Inventory;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly RecipeService _recipeService;

    public RecipesController(RecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet("variants/{variantId:int}")]
    public async Task<IActionResult> GetVariantRecipe(int variantId)
    {
        try
        {
            var recipe = await _recipeService.GetVariantRecipeAsync(variantId);
            return Ok(recipe);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("variants/{variantId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetVariantRecipe(int variantId, [FromBody] List<RecipeItemRequest> items)
    {
        try
        {
            var recipe = await _recipeService.SetVariantRecipeAsync(variantId, items);
            return Ok(recipe);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("toppings/{toppingId:int}")]
    public async Task<IActionResult> GetToppingRecipe(int toppingId)
    {
        try
        {
            var recipe = await _recipeService.GetToppingRecipeAsync(toppingId);
            return Ok(recipe);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("toppings/{toppingId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetToppingRecipe(int toppingId, [FromBody] List<RecipeItemRequest> items)
    {
        try
        {
            var recipe = await _recipeService.SetToppingRecipeAsync(toppingId, items);
            return Ok(recipe);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
