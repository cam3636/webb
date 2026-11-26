using Microsoft.EntityFrameworkCore;
using MilkyTeaChain.Api.Models;

namespace MilkyTeaChain.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPointTransaction> CustomerPointTransactions => Set<CustomerPointTransaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<BranchVariantPrice> BranchVariantPrices => Set<BranchVariantPrice>();
    public DbSet<BranchToppingPrice> BranchToppingPrices => Set<BranchToppingPrice>();
    public DbSet<PriceChangeHistory> PriceChangeHistories => Set<PriceChangeHistory>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<VariantRecipeItem> VariantRecipeItems => Set<VariantRecipeItem>();
    public DbSet<ToppingRecipeItem> ToppingRecipeItems => Set<ToppingRecipeItem>();
    public DbSet<BranchInventory> BranchInventories => Set<BranchInventory>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemTopping> OrderItemToppings => Set<OrderItemTopping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasIndex(b => b.Code).IsUnique();
            entity.Property(b => b.Code).HasMaxLength(20).IsRequired();
            entity.Property(b => b.Name).HasMaxLength(150).IsRequired();
            entity.Property(b => b.City).HasMaxLength(100);
            entity.Property(b => b.OpeningHours).HasMaxLength(100);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
            entity.HasOne(u => u.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.Phone).IsUnique();
            entity.Property(c => c.Phone).HasMaxLength(20).IsRequired();
            entity.Property(c => c.FullName).HasMaxLength(150).IsRequired();
        });

        modelBuilder.Entity<CustomerPointTransaction>(entity =>
        {
            entity.HasIndex(pt => pt.OrderId).IsUnique();
            entity.Property(pt => pt.OrderAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(pt => pt.Customer)
                .WithMany(c => c.PointTransactions)
                .HasForeignKey(pt => pt.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pt => pt.Order)
                .WithMany()
                .HasForeignKey(pt => pt.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pt => pt.Branch)
                .WithMany()
                .HasForeignKey(pt => pt.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(150).IsRequired();
            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasIndex(v => new { v.ProductId, v.Size }).IsUnique();
            entity.Property(v => v.Size).HasMaxLength(20).IsRequired();
            entity.Property(v => v.Price).HasColumnType("decimal(18,2)");
            entity.HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Topping>(entity =>
        {
            entity.HasIndex(t => t.Name).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Price).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<BranchVariantPrice>(entity =>
        {
            entity.HasIndex(bvp => new { bvp.BranchId, bvp.ProductVariantId }).IsUnique();
            entity.Property(bvp => bvp.Price).HasColumnType("decimal(18,2)");
            entity.HasOne(bvp => bvp.Branch)
                .WithMany(b => b.VariantPrices)
                .HasForeignKey(bvp => bvp.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(bvp => bvp.ProductVariant)
                .WithMany()
                .HasForeignKey(bvp => bvp.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BranchToppingPrice>(entity =>
        {
            entity.HasIndex(btp => new { btp.BranchId, btp.ToppingId }).IsUnique();
            entity.Property(btp => btp.Price).HasColumnType("decimal(18,2)");
            entity.HasOne(btp => btp.Branch)
                .WithMany(b => b.ToppingPrices)
                .HasForeignKey(btp => btp.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(btp => btp.Topping)
                .WithMany()
                .HasForeignKey(btp => btp.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceChangeHistory>(entity =>
        {
            entity.Property(ph => ph.OldPrice).HasColumnType("decimal(18,2)");
            entity.Property(ph => ph.NewPrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasIndex(i => i.Name).IsUnique();
            entity.Property(i => i.Name).HasMaxLength(100).IsRequired();
            entity.Property(i => i.Unit).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<VariantRecipeItem>(entity =>
        {
            entity.HasIndex(r => new { r.ProductVariantId, r.IngredientId }).IsUnique();
            entity.Property(r => r.Quantity).HasColumnType("decimal(18,2)");
            entity.HasOne(r => r.ProductVariant)
                .WithMany(v => v.RecipeItems)
                .HasForeignKey(r => r.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Ingredient)
                .WithMany(i => i.VariantRecipeItems)
                .HasForeignKey(r => r.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ToppingRecipeItem>(entity =>
        {
            entity.HasIndex(r => new { r.ToppingId, r.IngredientId }).IsUnique();
            entity.Property(r => r.Quantity).HasColumnType("decimal(18,2)");
            entity.HasOne(r => r.Topping)
                .WithMany(t => t.RecipeItems)
                .HasForeignKey(r => r.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Ingredient)
                .WithMany(i => i.ToppingRecipeItems)
                .HasForeignKey(r => r.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BranchInventory>(entity =>
        {
            entity.HasIndex(bi => new { bi.BranchId, bi.IngredientId }).IsUnique();
            entity.Property(bi => bi.Quantity).HasColumnType("decimal(18,2)");
            entity.Property(bi => bi.RowVersion).IsRowVersion();
            entity.ToTable(t => t.HasCheckConstraint("CK_BranchInventory_Quantity_NonNegative", "[Quantity] >= 0"));
            entity.HasOne(bi => bi.Branch)
                .WithMany(b => b.Inventories)
                .HasForeignKey(bi => bi.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(bi => bi.Ingredient)
                .WithMany(i => i.BranchInventories)
                .HasForeignKey(bi => bi.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReceipt>(entity =>
        {
            entity.HasOne(sr => sr.Branch)
                .WithMany(b => b.StockReceipts)
                .HasForeignKey(sr => sr.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(sr => sr.CreatedByUser)
                .WithMany(u => u.StockReceipts)
                .HasForeignKey(sr => sr.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReceiptItem>(entity =>
        {
            entity.Property(sri => sri.Quantity).HasColumnType("decimal(18,2)");
            entity.HasOne(sri => sri.StockReceipt)
                .WithMany(sr => sr.Items)
                .HasForeignKey(sri => sri.StockReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(sri => sri.Ingredient)
                .WithMany()
                .HasForeignKey(sri => sri.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(o => o.Branch)
                .WithMany(b => b.Orders)
                .HasForeignKey(o => o.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Cashier)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.CashierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(oi => oi.LineTotal).HasColumnType("decimal(18,2)");
            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(oi => oi.ProductVariant)
                .WithMany()
                .HasForeignKey(oi => oi.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItemTopping>(entity =>
        {
            entity.Property(oit => oit.Price).HasColumnType("decimal(18,2)");
            entity.HasOne(oit => oit.OrderItem)
                .WithMany(oi => oi.Toppings)
                .HasForeignKey(oit => oit.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(oit => oit.Topping)
                .WithMany()
                .HasForeignKey(oit => oit.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
