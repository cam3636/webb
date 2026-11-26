using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.DTOs.Admin;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Services;

public class BranchService
{
    private readonly AppDbContext _context;

    public BranchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Branch>> GetAllAsync(string? search = null, bool? isActive = null, string? city = null)
    {
        var query = _context.Branches.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(b =>
                b.Code.Contains(kw) ||
                b.Name.Contains(kw) ||
                b.City.Contains(kw) ||
                b.Address.Contains(kw) ||
                b.Phone.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim();
            query = query.Where(b => b.City == c);
        }

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(b => b.Id)
            .ToListAsync();
    }

    public async Task<Branch?> GetByIdAsync(int id)
    {
        return await _context.Branches.FindAsync(id);
    }

    public async Task<Branch> CreateAsync(SaveBranchRequest request)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _context.Branches.AnyAsync(b => b.Code == code))
        {
            throw new InvalidOperationException($"Mã chi nhánh '{code}' đã tồn tại.");
        }

        var branch = new Branch
        {
            Code = code,
            Name = request.Name.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? "Đà Nẵng" : request.City.Trim(),
            Address = request.Address?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            OpeningHours = string.IsNullOrWhiteSpace(request.OpeningHours) ? "07:00 - 22:30" : request.OpeningHours.Trim(),
            IsActive = request.IsActive ?? true
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        return branch;
    }

    public async Task<Branch> UpdateAsync(int id, SaveBranchRequest request)
    {
        var branch = await _context.Branches.FindAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {id}.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _context.Branches.AnyAsync(b => b.Code == code && b.Id != id))
        {
            throw new InvalidOperationException($"Mã chi nhánh '{code}' đã được sử dụng.");
        }

        branch.Code = code;
        branch.Name = request.Name.Trim();
        branch.City = string.IsNullOrWhiteSpace(request.City) ? branch.City : request.City.Trim();
        branch.Address = request.Address?.Trim() ?? string.Empty;
        branch.Phone = request.Phone?.Trim() ?? string.Empty;
        branch.OpeningHours = string.IsNullOrWhiteSpace(request.OpeningHours) ? "07:00 - 22:30" : request.OpeningHours.Trim();
        if (request.IsActive.HasValue)
        {
            branch.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();
        return branch;
    }

    public async Task<Branch> DeactivateAsync(int id)
    {
        var branch = await _context.Branches.FindAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {id}.");

        branch.IsActive = false;
        await _context.SaveChangesAsync();
        return branch;
    }

    public async Task<Branch> ActivateAsync(int id)
    {
        var branch = await _context.Branches.FindAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh mã {id}.");

        branch.IsActive = true;
        await _context.SaveChangesAsync();
        return branch;
    }
}
