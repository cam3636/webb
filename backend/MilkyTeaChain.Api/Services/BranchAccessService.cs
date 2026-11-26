using System.Security.Claims;
using MilkyTeaChain.Api.Enums;

namespace MilkyTeaChain.Api.Services;

public class BranchAccessService
{
    public int GetCurrentUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được mã người dùng từ token.");
        }
        return userId;
    }

    public UserRole GetCurrentUserRole(ClaimsPrincipal principal)
    {
        var roleStr = principal.FindFirst(ClaimTypes.Role)?.Value;
        if (!Enum.TryParse<UserRole>(roleStr, true, out var role))
        {
            throw new UnauthorizedAccessException("Vai trò người dùng không hợp lệ.");
        }
        return role;
    }

    public int? GetCurrentUserBranchId(ClaimsPrincipal principal)
    {
        var branchClaim = principal.FindFirst("BranchId")?.Value;
        if (int.TryParse(branchClaim, out var branchId))
        {
            return branchId;
        }
        return null;
    }

    /// <summary>
    /// Quy tắc phân quyền thao tác theo chi nhánh (4 vai trò chủ yếu):
    /// - Admin: toàn chuỗi (cần truyền branchId).
    /// - Manager, Cashier, Barista: chỉ thao tác tại chi nhánh được phân công.
    /// </summary>
    public int ResolveAccessibleBranchId(ClaimsPrincipal principal, int? requestedBranchId)
    {
        var role = GetCurrentUserRole(principal);
        var userBranchId = GetCurrentUserBranchId(principal);

        if (role == UserRole.Admin)
        {
            if (!requestedBranchId.HasValue)
            {
                throw new ArgumentException("Admin cần truyền mã chi nhánh (branchId) cho thao tác này.");
            }
            return requestedBranchId.Value;
        }

        if (!userBranchId.HasValue)
        {
            throw new UnauthorizedAccessException("Tài khoản chưa được gán vào chi nhánh nào.");
        }

        if (requestedBranchId.HasValue && requestedBranchId.Value != userBranchId.Value)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập hoặc thao tác dữ liệu của chi nhánh khác.");
        }

        return userBranchId.Value;
    }

    /// <summary>
    /// Dùng cho báo cáo hoặc truy vấn đơn hàng:
    /// - Admin: được đọc toàn chuỗi (null) hoặc lọc theo bất kỳ branchId nào.
    /// - Manager, Cashier, Barista: chỉ xem được chi nhánh của mình.
    /// </summary>
    public int? ResolveReportBranchScope(ClaimsPrincipal principal, int? requestedBranchId)
    {
        var role = GetCurrentUserRole(principal);
        var userBranchId = GetCurrentUserBranchId(principal);

        if (role == UserRole.Admin)
        {
            return requestedBranchId; // null = toàn chuỗi
        }

        if (!userBranchId.HasValue)
        {
            throw new UnauthorizedAccessException("Tài khoản chưa được gán vào chi nhánh nào.");
        }

        if (requestedBranchId.HasValue && requestedBranchId.Value != userBranchId.Value)
        {
            throw new UnauthorizedAccessException("Bạn chỉ được xem dữ liệu của chi nhánh mình.");
        }

        return userBranchId.Value;
    }

    public void EnsureCanAccessBranch(ClaimsPrincipal principal, int targetBranchId)
    {
        var role = GetCurrentUserRole(principal);
        if (role == UserRole.Admin)
        {
            return;
        }

        var userBranchId = GetCurrentUserBranchId(principal);
        if (!userBranchId.HasValue || userBranchId.Value != targetBranchId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu của chi nhánh khác.");
        }
    }
}
