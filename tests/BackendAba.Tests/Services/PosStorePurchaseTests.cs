using BackendAba.Models;
using BackendAba.Services;
using BackendAba.Tests.Infrastructure;
using static BackendAba.Tests.Infrastructure.Requests;

namespace BackendAba.Tests.Services;

public sealed class PosStorePurchaseTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    private PosStore NewStore() => new(database.CreateContext());

    [Fact]
    public void Compra_de_bultos_de_producto_nuevo_crea_producto_proveedor_y_un_lote_por_bulto()
    {
        var records = NewStore().RegisterPurchase(Purchase(BagLine(bags: 3, purchaseCost: 900m)));

        var record = Assert.Single(records);
        Assert.Equal(PurchaseKind.Bag, record.Kind);
        Assert.Equal("Maiz quebrado", record.ProductName);
        Assert.Equal("Grano", record.Category);
        Assert.Equal(3, record.Bags);
        Assert.Null(record.Quantity);
        Assert.Equal(40m, record.MarkedKg);
        Assert.Equal(900m, record.Investment);
        Assert.Equal("Granos del Norte", record.SupplierName);
        Assert.Equal("5551234567", record.SupplierPhone);

        using var db = database.CreateContext();
        Assert.Single(db.Products);
        Assert.Single(db.Suppliers);
        var lots = db.BagLots.ToList();
        Assert.Equal(3, lots.Count);
        Assert.All(lots, lot =>
        {
            Assert.Equal(300m, lot.PurchaseCost);
            Assert.Equal(BagStatus.Sealed, lot.Status);
            Assert.Equal(40m, lot.RemainingKg);
            Assert.Equal(record.ProductId, lot.ProductId);
        });
    }

    [Fact]
    public void Compra_de_piezas_crea_un_lote_de_piezas()
    {
        var record = Assert.Single(NewStore().RegisterPurchase(Purchase(UnitLine(quantity: 10m, purchaseCost: 500m, salePricePiece: 80m))));

        Assert.Equal(PurchaseKind.Unit, record.Kind);
        Assert.Null(record.Bags);
        Assert.Equal(10m, record.Quantity);
        Assert.Null(record.MarkedKg);

        using var db = database.CreateContext();
        var lot = Assert.Single(db.UnitLots);
        Assert.Equal(10m, lot.Quantity);
        Assert.Equal(10m, lot.RemainingQuantity);
        Assert.Equal(500m, lot.PurchaseCost);
        Assert.Equal(80m, lot.SalePricePiece);
        Assert.Empty(db.BagLots);
    }

    [Fact]
    public void Una_compra_con_bultos_y_piezas_devuelve_un_registro_por_linea()
    {
        var records = NewStore().RegisterPurchase(Purchase(BagLine(), UnitLine()));

        Assert.Equal([PurchaseKind.Bag, PurchaseKind.Unit], records.Select(record => record.Kind));
        using var db = database.CreateContext();
        Assert.Equal(2, db.Products.Count());
        Assert.Equal(2, db.Purchases.Count());
    }

    [Fact]
    public void Reutiliza_producto_y_proveedor_por_nombre_sin_importar_mayusculas_ni_espacios()
    {
        NewStore().RegisterPurchase(Purchase("Granos del Norte", "555", BagLine(productName: "Maiz quebrado")));
        NewStore().RegisterPurchase(Purchase("  granos DEL norte ", "", BagLine(productName: "  MAIZ QUEBRADO ")));

        using var db = database.CreateContext();
        var product = Assert.Single(db.Products);
        Assert.Equal("Maiz quebrado", product.Name);
        var supplier = Assert.Single(db.Suppliers);
        Assert.Equal("555", supplier.Phone);
        Assert.Equal(4, db.BagLots.Count(lot => lot.ProductId == product.Id));
    }

    [Fact]
    public void Actualiza_el_telefono_de_un_proveedor_existente_si_llega_uno_distinto()
    {
        NewStore().RegisterPurchase(Purchase("Granos del Norte", "555", BagLine()));
        NewStore().RegisterPurchase(Purchase("Granos del Norte", "777", BagLine()));

        using var db = database.CreateContext();
        Assert.Equal("777", Assert.Single(db.Suppliers).Phone);
    }

    [Fact]
    public void Sin_nombre_de_proveedor_usa_Sin_proveedor_y_sin_categoria_usa_General()
    {
        var record = Assert.Single(NewStore().RegisterPurchase(Purchase("  ", null, BagLine(category: null))));

        Assert.Equal("Sin proveedor", record.SupplierName);
        Assert.Equal(string.Empty, record.SupplierPhone);
        Assert.Equal("General", record.Category);
    }

    [Fact]
    public void Lista_las_compras_de_la_mas_reciente_a_la_mas_antigua_con_su_proveedor()
    {
        NewStore().RegisterPurchase(Purchase(BagLine(productName: "Primero")));
        Thread.Sleep(5);
        NewStore().RegisterPurchase(Purchase(BagLine(productName: "Segundo")));

        var purchases = NewStore().GetPurchases();

        Assert.Equal(["Segundo", "Primero"], purchases.Select(purchase => purchase.ProductName));
        Assert.All(purchases, purchase => Assert.Equal("Granos del Norte", purchase.SupplierName));
    }
}
