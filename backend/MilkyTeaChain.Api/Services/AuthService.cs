using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Auth;

namespace MilkyTeaChain.Api.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(AppDbContext context, JwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || !UserService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = _jwtTokenService.GenerateToken(user);

        return new LoginResponse
        {
            AccessToken = token,
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            BranchId = user.BranchId
        };
    }
}
