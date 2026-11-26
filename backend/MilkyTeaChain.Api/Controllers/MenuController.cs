using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Menu;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly MenuService _menuService;

    public MenuController(MenuService menuService)
    {
        _menuService = menuService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFullMenu([FromQuery] int? branchId)
    {
        var menu = await _menuService.GetFullMenuAsync(branchId);
        return Ok(menu);
    }

    [HttpPost("categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCategoryRequest request)
    {
        try
        {
            var category = await _menuService.CreateCategoryAsync(request);
            return Ok(category);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("products")]
    [Authorize(Roles = "Admin")]
    [HttpPut("products/{productId:int}")]
    public async Task<IActionResult> SaveProduct([FromBody] SaveProductRequest request, [FromRoute] int? productId = null)
    {
        try
        {
            var product = await _menuService.SaveProductAsync(request, productId);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("products/{productId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveProduct(int productId)
    {
        try
        {
            await _menuService.RemoveProductAsync(productId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin đặt giá chung toàn chuỗi cho biến thể (Size) của món.
    /// </summary>
    [HttpPost("variants")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveVariant([FromBody] SaveVariantRequest request)
    {
        try
        {
            var variant = await _menuService.SaveVariantAsync(User, request);
            return Ok(variant);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin đặt giá chung toàn chuỗi cho Topping.
    /// </summary>
    [HttpPost("toppings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveTopping([FromBody] SaveToppingRequest request)
    {
        var topping = await _menuService.SaveToppingAsync(User, request);
        return Ok(topping);
    }

    /// <summary>
    /// Manager đặt giá riêng hoặc chọn "Dùng lại giá chung" cho biến thể món tại chi nhánh phụ trách (Admin đặt được cho mọi chi nhánh).
    /// PUT /api/branches/{branchId}/menu/variants/{variantId}/price
    /// </summary>
    [HttpPut("/api/branches/{branchId:int}/menu/variants/{variantId:int}/price")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetBranchVariantPrice(int branchId, int variantId, [FromBody] SetBranchPriceRequest request)
    {
        try
        {
            var result = await _menuService.SetBranchVariantPriceAsync(User, branchId, variantId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Manager đặt giá riêng hoặc chọn "Dùng lại giá chung" cho Topping tại chi nhánh phụ trách (Admin đặt được cho mọi chi nhánh).
    /// PUT /api/branches/{branchId}/menu/toppings/{toppingId}/price
    /// </summary>
    [HttpPut("/api/branches/{branchId:int}/menu/toppings/{toppingId:int}/price")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetBranchToppingPrice(int branchId, int toppingId, [FromBody] SetBranchPriceRequest request)
    {
        try
        {
            var result = await _menuService.SetBranchToppingPriceAsync(User, branchId, toppingId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xem lịch sử đổi giá (gồm người sửa, thời gian, phạm vi, giá cũ, giá mới).
    /// GET /api/menu/price-history
    /// </summary>
    [HttpGet("price-history")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetPriceHistory([FromQuery] int? branchId)
    {
        try
        {
            var history = await _menuService.GetPriceHistoryAsync(User, branchId);
            return Ok(history);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
