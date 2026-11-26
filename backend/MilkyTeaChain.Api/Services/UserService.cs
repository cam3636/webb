using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Admin;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserResponse>> GetAllUsersAsync()
    {
        return await _context.Users
            .Include(u => u.Branch)
            .OrderBy(u => u.Id)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                IsActive = u.IsActive
            })
            .ToListAsync();
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var username = request.Username.Trim();
        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            throw new InvalidOperationException($"Tên đăng nhập '{username}' đã tồn tại.");
        }

        if (request.Role != UserRole.Admin)
        {
            if (!request.BranchId.HasValue)
            {
                throw new ArgumentException($"Tài khoản vai trò {request.Role} bắt buộc phải gán vào một chi nhánh.");
            }

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId.Value && b.IsActive);
            if (!branchExists)
            {
                throw new KeyNotFoundException($"Chi nhánh mã {request.BranchId.Value} không tồn tại hoặc đã ngừng hoạt động.");
            }
        }

        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            BranchId = request.Role == UserRole.Admin ? null : request.BranchId,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var branch = user.BranchId.HasValue
            ? await _context.Branches.FindAsync(user.BranchId.Value)
            : null;

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            BranchId = user.BranchId,
            BranchName = branch?.Name,
            IsActive = user.IsActive
        };
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
