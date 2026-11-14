using BackendAba.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAba.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
    public DbSet<BagLotEntity> BagLots => Set<BagLotEntity>();
    public DbSet<UnitLotEntity> UnitLots => Set<UnitLotEntity>();
    public DbSet<PurchaseEntity> Purchases => Set<PurchaseEntity>();
    public DbSet<SaleEntity> Sales => Set<SaleEntity>();
    public DbSet<SaleLineEntity> SaleLines => Set<SaleLineEntity>();
    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(160).IsRequired();
            entity.Property(product => product.Category).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<SupplierEntity>(entity =>
        {
            entity.HasKey(supplier => supplier.Id);
            entity.Property(supplier => supplier.Name).HasMaxLength(160).IsRequired();
            entity.Property(supplier => supplier.Phone).HasMaxLength(40);
        });

        modelBuilder.Entity<BagLotEntity>(entity =>
        {
            entity.HasKey(lot => lot.Id);
            entity.Property(lot => lot.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(lot => lot.Product)
                .WithMany(product => product.Lots)
                .HasForeignKey(lot => lot.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(lot => lot.Supplier)
                .WithMany()
                .HasForeignKey(lot => lot.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnitLotEntity>(entity =>
        {
            entity.HasKey(lot => lot.Id);
            entity.HasOne(lot => lot.Product)
                .WithMany(product => product.UnitLots)
                .HasForeignKey(lot => lot.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(lot => lot.Supplier)
                .WithMany()
                .HasForeignKey(lot => lot.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseEntity>(entity =>
        {
            entity.HasKey(purchase => purchase.Id);
            entity.Property(purchase => purchase.Kind).HasConversion<string>().HasMaxLength(10);
            entity.Property(purchase => purchase.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(purchase => purchase.Category).HasMaxLength(120);
            entity.HasOne(purchase => purchase.Product)
                .WithMany()
                .HasForeignKey(purchase => purchase.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(purchase => purchase.Supplier)
                .WithMany()
                .HasForeignKey(purchase => purchase.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleEntity>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.Property(sale => sale.Customer).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<SaleLineEntity>(entity =>
        {
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(line => line.Unit).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(line => line.Sale)
                .WithMany(sale => sale.Lines)
                .HasForeignKey(line => line.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(line => line.Product)
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseEntity>(entity =>
        {
            entity.HasKey(expense => expense.Id);
            entity.Property(expense => expense.Concept).HasMaxLength(160).IsRequired();
            entity.Property(expense => expense.Category).HasMaxLength(120).IsRequired();
            entity.Property(expense => expense.Area).HasConversion<string>().HasMaxLength(20);
            entity.Property(expense => expense.Notes).HasMaxLength(400);
        });
    }
}

public sealed class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int MinBags { get; set; }
    public ProductKind Kind { get; set; }
    public List<BagLotEntity> Lots { get; set; } = new();
    public List<UnitLotEntity> UnitLots { get; set; } = new();
}

public sealed class SupplierEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public sealed class BagLotEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity? Supplier { get; set; }
    public DateTime PurchasedAt { get; set; }
    public decimal MarkedKg { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalePriceBag { get; set; }
    public decimal SalePriceKg { get; set; }
    public BagStatus Status { get; set; }
    public decimal RemainingKg { get; set; }
}

public sealed class UnitLotEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity? Supplier { get; set; }
    public DateTime PurchasedAt { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalePricePiece { get; set; }
    public decimal RemainingQuantity { get; set; }
}

public sealed class PurchaseEntity
{
    public Guid Id { get; set; }
    public DateTime PurchasedAt { get; set; }
    public PurchaseKind Kind { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public SupplierEntity? Supplier { get; set; }
    public int? Bags { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? MarkedKg { get; set; }
    public decimal Investment { get; set; }
}

public sealed class SaleEntity
{
    public Guid Id { get; set; }
    public DateTime SoldAt { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public List<SaleLineEntity> Lines { get; set; } = new();
}

public sealed class SaleLineEntity
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public SaleEntity? Sale { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public SaleUnit Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Cost { get; set; }
    public decimal Total { get; set; }
    public decimal Profit { get; set; }
}

public sealed class ExpenseEntity
{
    public Guid Id { get; set; }
    public DateTime ExpenseAt { get; set; }
    public string Concept { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ExpenseArea Area { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
