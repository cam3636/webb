using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Customers;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Tra cứu khách hàng bằng số điện thoại tại quầy (Cashier, Manager, Admin).
    /// Barista không được phép truy cập danh bạ khách hàng.
    /// GET /api/customers/lookup?phone=...
    /// </summary>
    [HttpGet("lookup")]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> LookupByPhone([FromQuery] string phone)
    {
        try
        {
            var customer = await _customerService.LookupByPhoneAsync(phone);
            if (customer == null)
            {
                return NotFound(new { message = "Chưa tìm thấy khách hàng với số điện thoại này." });
            }
            return Ok(customer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Tạo nhanh khách hàng thân thiết mới bằng họ tên và số điện thoại ngay tại quầy.
    /// POST /api/customers
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> CreateCustomer([FromBody] SaveCustomerRequest request)
    {
        try
        {
            var customer = await _customerService.CreateOrGetCustomerAsync(request);
            return Ok(customer);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Danh sách khách hàng thân thiết (Admin xem toàn chuỗi, Manager xem thống kê giao dịch tại chi nhánh mình).
    /// Cashier chỉ được tra cứu từng khách qua /lookup, không được xem toàn bộ danh bạ.
    /// GET /api/customers
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] int? branchId, [FromQuery] string? search)
    {
        try
        {
            var list = await _customerService.GetCustomersListAsync(User, branchId, search);
            return Ok(list);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xem lịch sử tích điểm và mua hàng của một khách hàng.
    /// Admin xem toàn chuỗi, Manager chỉ xem giao dịch của chi nhánh mình.
    /// GET /api/customers/{customerId}/transactions
    /// </summary>
    [HttpGet("{customerId:int}/transactions")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetTransactions(int customerId, [FromQuery] int? branchId)
    {
        try
        {
            var data = await _customerService.GetCustomerTransactionsAsync(User, customerId, branchId);
            return Ok(data);
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
}
