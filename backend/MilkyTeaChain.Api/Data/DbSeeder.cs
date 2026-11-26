using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Enums;
using MilkyTeaChain.Api.Models;
using MilkyTeaChain.Api.Services;

namespace MilkyTeaChain.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        IHostEnvironment environment,
        ILogger? logger = null)
    {
        await context.Database.MigrateAsync();

        // 1. Cập nhật dữ liệu mẫu A/B hiện có sang DN01, DN02 (giữ nguyên Id để bảo toàn liên kết với nhân viên, hóa đơn và tồn kho cũ)
        //    và tạo thêm HUE01 nếu chưa có. Chạy lại nhiều lần tuyệt đối không tạo trùng chi nhánh.
        var branch1 = await context.Branches.FirstOrDefaultAsync(b => b.Code == "DN01" || b.Code == "BR-A");
        if (branch1 == null)
        {
            branch1 = new Branch
            {
                Code = "DN01",
                Name = "Milky Tea Nguyễn Văn Linh",
                City = "Đà Nẵng",
                Address = "đường Nguyễn Văn Linh, Đà Nẵng",
                Phone = "0901000001",
                OpeningHours = "07:00 - 22:30",
                IsActive = true
            };
            context.Branches.Add(branch1);
            await context.SaveChangesAsync();
        }
        else if (branch1.Code == "BR-A" || string.IsNullOrWhiteSpace(branch1.City))
        {
            branch1.Code = "DN01";
            branch1.Name = "Milky Tea Nguyễn Văn Linh";
            branch1.City = "Đà Nẵng";
            branch1.Address = "đường Nguyễn Văn Linh, Đà Nẵng";
            await context.SaveChangesAsync();
        }

        var branch2 = await context.Branches.FirstOrDefaultAsync(b => b.Code == "DN02" || b.Code == "BR-B");
        if (branch2 == null)
        {
            branch2 = new Branch
            {
                Code = "DN02",
                Name = "Milky Tea Nguyễn Tất Thành",
                City = "Đà Nẵng",
                Address = "đường Nguyễn Tất Thành, Đà Nẵng",
                Phone = "0901000002",
                OpeningHours = "07:00 - 22:30",
                IsActive = true
            };
            context.Branches.Add(branch2);
            await context.SaveChangesAsync();
        }
        else if (branch2.Code == "BR-B" || string.IsNullOrWhiteSpace(branch2.City))
        {
            branch2.Code = "DN02";
            branch2.Name = "Milky Tea Nguyễn Tất Thành";
            branch2.City = "Đà Nẵng";
            branch2.Address = "đường Nguyễn Tất Thành, Đà Nẵng";
            await context.SaveChangesAsync();
        }

        var branch3 = await context.Branches.FirstOrDefaultAsync(b => b.Code == "HUE01");
        if (branch3 == null)
        {
            branch3 = new Branch
            {
                Code = "HUE01",
                Name = "Milky Tea Hùng Vương",
                City = "Huế",
                Address = "đường Hùng Vương, Huế",
                Phone = "0901000003",
                OpeningHours = "07:00 - 22:30",
                IsActive = true
            };
            context.Branches.Add(branch3);
            await context.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(branch3.City))
        {
            branch3.City = "Huế";
            await context.SaveChangesAsync();
        }

        // 2. Chỉ tạo tài khoản mẫu trong môi trường Development (4 vai trò chủ yếu: Admin, Manager, Cashier, Barista)
        if (environment.IsDevelopment())
        {
            var obsoleteUsers = await context.Users
                .Where(u => u.Username == "warehouse_a" || u.Username == "accountant")
                .ToListAsync();

            foreach (var obs in obsoleteUsers)
            {
                bool hasReceipts = await context.StockReceipts.AnyAsync(r => r.CreatedByUserId == obs.Id);
                bool hasOrders = await context.Orders.AnyAsync(o => o.CashierId == obs.Id);
                if (!hasReceipts && !hasOrders)
                {
                    context.Users.Remove(obs);
                }
                else
                {
                    obs.Role = UserRole.Manager;
                    obs.BranchId ??= branch1.Id;
                }
            }
            if (obsoleteUsers.Count > 0)
            {
                await context.SaveChangesAsync();
            }

            // Cấu hình tài khoản Admin demo: cam / cam123
            var existingCam = await context.Users.FirstOrDefaultAsync(u => u.Username == "cam");
            if (existingCam == null)
            {
                context.Users.Add(new User
                {
                    Username = "cam",
                    PasswordHash = UserService.HashPassword("cam123"),
                    FullName = "Quản trị viên Cam",
                    Role = UserRole.Admin,
                    BranchId = null,
                    IsActive = true
                });
                await context.SaveChangesAsync();
                logger?.LogInformation("Đã khởi tạo tài khoản Admin demo 'cam'.");
            }
            else if (existingCam.Role != UserRole.Admin)
            {
                logger?.LogWarning(
                    "XUNG ĐỘT TÀI KHOẢN DEMO: Tên đăng nhập 'cam' đã tồn tại với vai trò '{ExistingRole}'. Giữ nguyên quyền và mật khẩu hiện tại của tài khoản này.",
                    existingCam.Role);
            }

            // Tài khoản mẫu cho 4 vai trò chủ yếu gắn với các chi nhánh
            await EnsureDemoUserAsync(context, "admin", "Admin@123", "Quản trị viên chuỗi", UserRole.Admin, null);
            await EnsureDemoUserAsync(context, "manager_a", "Manager@123", "Quản lý Milky Tea Nguyễn Văn Linh", UserRole.Manager, branch1.Id);
            await EnsureDemoUserAsync(context, "cashier_a", "Cashier@123", "Thu ngân Milky Tea Nguyễn Văn Linh", UserRole.Cashier, branch1.Id);
            await EnsureDemoUserAsync(context, "barista_a", "Barista@123", "Pha chế Milky Tea Nguyễn Văn Linh", UserRole.Barista, branch1.Id, forceRoleUpdate: true);
            await EnsureDemoUserAsync(context, "manager_b", "Manager@123", "Quản lý Milky Tea Nguyễn Tất Thành", UserRole.Manager, branch2.Id);
            await EnsureDemoUserAsync(context, "cashier_b", "Cashier@123", "Thu ngân Milky Tea Nguyễn Tất Thành", UserRole.Cashier, branch2.Id);
            await EnsureDemoUserAsync(context, "barista_b", "Barista@123", "Pha chế Milky Tea Nguyễn Tất Thành", UserRole.Barista, branch2.Id, forceRoleUpdate: true);
        }

        // 3. Tạo danh mục, món, biến thể kích cỡ và topping
        if (!await context.Categories.AnyAsync())
        {
            var catMilkTea = new Category { Name = "Trà sữa", IsActive = true };
            var catCoffee = new Category { Name = "Cà phê", IsActive = true };
            context.Categories.AddRange(catMilkTea, catCoffee);
            await context.SaveChangesAsync();

            var classicMilkTea = new Product
            {
                CategoryId = catMilkTea.Id,
                Name = "Trà sữa truyền thống",
                IsActive = true
            };
            context.Products.Add(classicMilkTea);
            await context.SaveChangesAsync();

            var variantM = new ProductVariant
            {
                ProductId = classicMilkTea.Id,
                Size = "M",
                Price = 30000m,
                IsActive = true
            };
            var variantL = new ProductVariant
            {
                ProductId = classicMilkTea.Id,
                Size = "L",
                Price = 35000m,
                IsActive = true
            };
            context.ProductVariants.AddRange(variantM, variantL);
            await context.SaveChangesAsync();
        }

        if (!await context.Toppings.AnyAsync())
        {
            context.Toppings.AddRange(
                new Topping { Name = "Trân châu đen", Price = 5000m, IsActive = true },
                new Topping { Name = "Thạch dừa", Price = 5000m, IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        // 4. Tạo nguyên liệu (g hoặc ml)
        if (!await context.Ingredients.AnyAsync())
        {
            context.Ingredients.AddRange(
                new Ingredient { Name = "Cốt trà đen", Unit = "ml", IsActive = true },
                new Ingredient { Name = "Sữa tươi", Unit = "ml", IsActive = true },
                new Ingredient { Name = "Đường nước", Unit = "ml", IsActive = true },
                new Ingredient { Name = "Hạt trân châu", Unit = "g", IsActive = true },
                new Ingredient { Name = "Sợi thạch dừa", Unit = "g", IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        var tea = await context.Ingredients.FirstAsync(i => i.Name == "Cốt trà đen");
        var milk = await context.Ingredients.FirstAsync(i => i.Name == "Sữa tươi");
        var sugar = await context.Ingredients.FirstAsync(i => i.Name == "Đường nước");
        var pearlIng = await context.Ingredients.FirstAsync(i => i.Name == "Hạt trân châu");
        var jellyIng = await context.Ingredients.FirstAsync(i => i.Name == "Sợi thạch dừa");

        // 5. Tạo công thức cho biến thể và topping
        if (!await context.VariantRecipeItems.AnyAsync())
        {
            var varM = await context.ProductVariants.FirstAsync(v => v.Size == "M");
            var varL = await context.ProductVariants.FirstAsync(v => v.Size == "L");

            context.VariantRecipeItems.AddRange(
                new VariantRecipeItem { ProductVariantId = varM.Id, IngredientId = tea.Id, Quantity = 150m },
                new VariantRecipeItem { ProductVariantId = varM.Id, IngredientId = milk.Id, Quantity = 100m },
                new VariantRecipeItem { ProductVariantId = varM.Id, IngredientId = sugar.Id, Quantity = 20m },
                new VariantRecipeItem { ProductVariantId = varL.Id, IngredientId = tea.Id, Quantity = 200m },
                new VariantRecipeItem { ProductVariantId = varL.Id, IngredientId = milk.Id, Quantity = 150m },
                new VariantRecipeItem { ProductVariantId = varL.Id, IngredientId = sugar.Id, Quantity = 30m }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.ToppingRecipeItems.AnyAsync())
        {
            var pearlTop = await context.Toppings.FirstAsync(t => t.Name == "Trân châu đen");
            var jellyTop = await context.Toppings.FirstAsync(t => t.Name == "Thạch dừa");

            context.ToppingRecipeItems.AddRange(
                new ToppingRecipeItem { ToppingId = pearlTop.Id, IngredientId = pearlIng.Id, Quantity = 50m },
                new ToppingRecipeItem { ToppingId = jellyTop.Id, IngredientId = jellyIng.Id, Quantity = 50m }
            );
            await context.SaveChangesAsync();
        }

        // 6. Đảm bảo tất cả các chi nhánh mẫu (DN01, DN02, HUE01) đều có tồn kho nguyên liệu
        var allIngredients = await context.Ingredients.ToListAsync();
        foreach (var branch in new[] { branch1, branch2, branch3 })
        {
            var existingInvIngIds = await context.BranchInventories
                .Where(bi => bi.BranchId == branch.Id)
                .Select(bi => bi.IngredientId)
                .ToListAsync();

            foreach (var ing in allIngredients.Where(i => !existingInvIngIds.Contains(i.Id)))
            {
                context.BranchInventories.Add(new BranchInventory
                {
                    BranchId = branch.Id,
                    IngredientId = ing.Id,
                    Quantity = 5000m
                });
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureDemoUserAsync(
        AppDbContext context,
        string username,
        string password,
        string fullName,
        UserRole role,
        int? branchId,
        bool forceRoleUpdate = false)
    {
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (existing == null)
        {
            context.Users.Add(new User
            {
                Username = username,
                PasswordHash = UserService.HashPassword(password),
                FullName = fullName,
                Role = role,
                BranchId = branchId,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
        else if (forceRoleUpdate && existing.Role != role)
        {
            existing.Role = role;
            await context.SaveChangesAsync();
        }
    }
}
