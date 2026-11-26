using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.DTOs.Admin;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly BranchService _branchService;

    public BranchesController(BranchService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] string? city)
    {
        var branches = await _branchService.GetAllAsync(search, isActive, city);
        return Ok(branches);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var branch = await _branchService.GetByIdAsync(id);
        if (branch == null)
        {
            return NotFound(new { message = $"Không tìm thấy chi nhánh mã {id}." });
        }
        return Ok(branch);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] SaveBranchRequest request)
    {
        try
        {
            var branch = await _branchService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = branch.Id }, branch);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveBranchRequest request)
    {
        try
        {
            var branch = await _branchService.UpdateAsync(id, request);
            return Ok(branch);
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

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            var branch = await _branchService.DeactivateAsync(id);
            return Ok(branch);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        try
        {
            var branch = await _branchService.ActivateAsync(id);
            return Ok(branch);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
