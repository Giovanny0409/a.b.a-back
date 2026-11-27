using BackendAba.Data;
using BackendAba.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BackendAba.Tests.Services;

/// <summary>
/// La migración AddProductKind sobre una base con datos del esquema anterior: cada producto toma el tipo
/// de sus lotes.
/// </summary>
public sealed class AddProductKindMigrationTests : IDisposable
{
    private const string InitialSchema = "20260831013131_InitialSchema";
    private const string Supplier = "20000000-0000-0000-0000-000000000001";
    private const string Feed = "10000000-0000-0000-0000-000000000001";
    private const string Feeder = "10000000-0000-0000-0000-000000000002";
    private const string Mixed = "10000000-0000-0000-0000-000000000003";
    private const string WithoutLots = "10000000-0000-0000-0000-000000000004";

    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public AddProductKindMigrationTests() => connection.Open();

    public void Dispose() => connection.Dispose();

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    [Fact]
    public void Los_productos_existentes_toman_el_tipo_de_sus_lotes()
    {
        using (var db = CreateContext())
        {
            db.GetService<IMigrator>().Migrate(InitialSchema);
            db.Database.ExecuteSqlRaw($"""
                INSERT INTO Suppliers (Id, Name, Phone) VALUES ('{Supplier}', 'Proveedor', '');
                INSERT INTO Products (Id, Name, Category, MinBags) VALUES
                    ('{Feed}', 'Alimento', 'Alimento', 0),
                    ('{Feeder}', 'Comedero', 'Comedero', 0),
                    ('{Mixed}', 'Mixto', 'General', 0),
                    ('{WithoutLots}', 'Sin lotes', 'General', 0);
                INSERT INTO BagLots (Id, ProductId, SupplierId, PurchasedAt, MarkedKg, PurchaseCost, SalePriceBag, SalePriceKg, Status, RemainingKg) VALUES
                    ('30000000-0000-0000-0000-000000000001', '{Feed}', '{Supplier}', '2026-01-01', '40', '500', '600', '16', 'Sealed', '40'),
                    ('30000000-0000-0000-0000-000000000002', '{Mixed}', '{Supplier}', '2026-01-01', '40', '500', '600', '16', 'Sealed', '40');
                INSERT INTO UnitLots (Id, ProductId, SupplierId, PurchasedAt, Quantity, PurchaseCost, SalePricePiece, RemainingQuantity) VALUES
                    ('40000000-0000-0000-0000-000000000001', '{Feeder}', '{Supplier}', '2026-01-01', '2', '90', '75', '2'),
                    ('40000000-0000-0000-0000-000000000002', '{Mixed}', '{Supplier}', '2026-01-01', '2', '90', '75', '2');
                """);

            db.Database.Migrate();
        }

        using var migrated = CreateContext();
        var kinds = migrated.Products.ToDictionary(product => product.Name, product => product.Kind);
        Assert.Equal(ProductKind.Bag, kinds["Alimento"]);
        Assert.Equal(ProductKind.Unit, kinds["Comedero"]);
        Assert.Equal(ProductKind.Bag, kinds["Mixto"]);
        Assert.Equal(ProductKind.Bag, kinds["Sin lotes"]);
    }
}
