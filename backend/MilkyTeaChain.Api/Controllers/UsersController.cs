using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Admin;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            var created = await _userService.CreateUserAsync(request);
            return Ok(created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
        {
            return BadRequest(new { message = "Bạn không thể đổi vai trò của tài khoản đang đăng nhập." });
        }

        try
        {
            await _userService.UpdateRoleAsync(id, request.Role!.Value, request.BranchId);
            return NoContent();
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
