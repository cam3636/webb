using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Orders;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly PaymentService _paymentService;

    public OrdersController(OrderService orderService, PaymentService paymentService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Cashier,Barista")]
    public async Task<IActionResult> GetOrders([FromQuery] int? branchId)
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(User, branchId);
            return Ok(orders);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("/api/branches/{branchId:int}/barista/orders")]
    [Authorize(Roles = "Admin,Barista")]
    public async Task<IActionResult> GetBaristaOrders(int branchId)
    {
        try
        {
            var orders = await _orderService.GetBaristaPaidOrdersAsync(User, branchId);
            return Ok(orders);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("{orderId:int}")]
    [Authorize(Roles = "Admin,Manager,Cashier,Barista")]
    public async Task<IActionResult> GetOrderById(int orderId)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(User, orderId);
            return Ok(order);
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

    [HttpPost("/api/branches/{branchId:int}/orders")]
    [Authorize(Roles = "Admin,Cashier")]
    public async Task<IActionResult> CreateOrderForBranch(int branchId, [FromBody] CreateOrderRequest request)
    {
        request.BranchId = branchId;
        return await CreateOrder(request);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Cashier")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var order = await _orderService.CreateOrderAsync(User, request);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = order.OrderId }, order);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("PRICE_CHANGED:"))
        {
            return Conflict(new { code = "PRICE_CHANGED", message = ex.Message.Replace("PRICE_CHANGED: ", "") });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId:int}/payment")]
    [HttpPost("{orderId:int}/pay")]
    [Authorize(Roles = "Admin,Cashier")]
    public async Task<IActionResult> PayOrder(int orderId)
    {
        try
        {
            var paidOrder = await _paymentService.PayOrderAsync(User, orderId);
            return Ok(paidOrder);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId:int}/start-prepare")]
    [Authorize(Roles = "Admin,Barista")]
    public async Task<IActionResult> StartPreparing(int orderId)
    {
        try
        {
            var order = await _orderService.StartPreparingOrderAsync(User, orderId);
            return Ok(order);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId:int}/prepare")]
    [Authorize(Roles = "Admin,Barista")]
    public async Task<IActionResult> MarkPrepared(int orderId)
    {
        try
        {
            var order = await _orderService.MarkOrderPreparedAsync(User, orderId);
            return Ok(order);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
