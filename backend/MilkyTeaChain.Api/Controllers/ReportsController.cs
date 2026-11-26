using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Xem doanh thu, số đơn đã thanh toán và số lượng bán theo món của chi nhánh.
    /// GET /api/branches/{branchId}/reports/revenue?from=YYYY-MM-DD&to=YYYY-MM-DD
    /// </summary>
    [HttpGet("/api/branches/{branchId:int}/reports/revenue")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetBranchRevenueReport(
        int branchId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var report = await _reportService.GetRevenueReportAsync(
                User,
                branchId,
                from ?? fromDate,
                to ?? toDate);
            return Ok(report);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin xem báo cáo tổng hợp toàn chuỗi (hoặc Manager xem chi nhánh mình).
    /// GET /api/reports/revenue?from=YYYY-MM-DD&to=YYYY-MM-DD
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] int? branchId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var report = await _reportService.GetRevenueReportAsync(
                User,
                branchId,
                from ?? fromDate,
                to ?? toDate);
            return Ok(report);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts(
        [FromQuery] int? branchId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var report = await _reportService.GetTopSellingProductsAsync(
                User,
                branchId,
                from ?? fromDate,
                to ?? toDate);
            return Ok(report);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
