using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Inventory;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("ingredients")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAllIngredients()
    {
        var ingredients = await _inventoryService.GetAllIngredientsAsync();
        return Ok(ingredients);
    }

    [HttpPost("ingredients")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveIngredient([FromBody] SaveIngredientRequest request)
    {
        try
        {
            var ingredient = await _inventoryService.SaveIngredientAsync(request);
            return Ok(ingredient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("/api/branches/{branchId:int}/inventory")]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> GetBranchInventoryByRoute(int branchId)
    {
        return await GetBranchInventory(branchId);
    }

    [HttpGet("stock")]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> GetBranchInventory([FromQuery] int? branchId)
    {
        try
        {
            var stock = await _inventoryService.GetBranchInventoryAsync(User, branchId);
            return Ok(stock);
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

    [HttpGet("/api/branches/{branchId:int}/inventory/history")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetBranchInventoryHistory(int branchId)
    {
        try
        {
            var history = await _inventoryService.GetInventoryHistoryAsync(User, branchId);
            return Ok(history);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("/api/branches/{branchId:int}/stock-receipts")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateStockReceiptByRoute(int branchId, [FromBody] CreateStockReceiptRequest request)
    {
        request.BranchId = branchId;
        return await CreateStockReceipt(request);
    }

    [HttpPost("receipts")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateStockReceipt([FromBody] CreateStockReceiptRequest request)
    {
        try
        {
            var receipt = await _inventoryService.CreateStockReceiptAsync(User, request);
            return Ok(receipt);
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
}
