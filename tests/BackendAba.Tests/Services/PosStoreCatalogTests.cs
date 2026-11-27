using BackendAba.Models;
using BackendAba.Services;
using BackendAba.Tests.Infrastructure;
using static BackendAba.Tests.Infrastructure.Requests;

namespace BackendAba.Tests.Services;

public sealed class PosStoreCatalogTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    private PosStore NewStore() => new(database.CreateContext());

    [Fact]
    public void Inventario_separa_bultos_sellados_kilos_abiertos_y_piezas()
    {
        var bagId = NewStore().RegisterPurchase(Purchase(BagLine(bags: 3, purchaseCost: 900m)))[0].ProductId;
        NewStore().RegisterPurchase(Purchase(UnitLine(quantity: 4m)));
        NewStore().RegisterSaleBatch(Sale(Line(bagId, SaleUnit.Kg, 15)));

        var inventory = NewStore().GetInventory();

        var bags = Assert.Single(inventory, item => item.Product.Name == "Maiz quebrado");
        Assert.Equal(2, bags.SealedBags);
        Assert.Equal(25m, bags.OpenKg);
        Assert.Equal(0m, bags.QuantityTotal);
        var pieces = Assert.Single(inventory, item => item.Product.Name == "Correa");
        Assert.Equal(0, pieces.SealedBags);
        Assert.Equal(4m, pieces.QuantityTotal);
    }

    [Fact]
    public void Marca_por_surtir_cuando_los_bultos_sellados_no_pasan_del_minimo()
    {
        var product = NewStore().AddProduct(new CreateProductRequest("Alimento engorda", "Balanceado", 2));
        NewStore().RegisterPurchase(Purchase(BagLine(productName: "Alimento engorda", bags: 2)));
        Assert.True(Assert.Single(NewStore().GetInventory()).NeedsRestock);

        NewStore().RegisterPurchase(Purchase(BagLine(productName: "Alimento engorda", bags: 1)));
        var item = Assert.Single(NewStore().GetInventory());
        Assert.Equal(product.Id, item.Product.Id);
        Assert.False(item.NeedsRestock);
    }

    [Fact]
    public void Da_de_alta_productos_sin_espacios_y_los_lista_por_nombre()
    {
        NewStore().AddProduct(new CreateProductRequest("  Salvado ", " Forraje ", 3));
        NewStore().AddProduct(new CreateProductRequest("Alimento", "Balanceado", 1));

        var products = NewStore().GetProducts();

        Assert.Equal(["Alimento", "Salvado"], products.Select(product => product.Name));
        Assert.Equal("Forraje", products[1].Category);
        Assert.Equal(3, products[1].MinBags);
    }

    [Fact]
    public void Lista_los_lotes_con_su_proveedor()
    {
        NewStore().RegisterPurchase(Purchase(BagLine(bags: 2)));

        var lots = NewStore().GetLots();

        Assert.Equal(2, lots.Count);
        Assert.All(lots, lot =>
        {
            Assert.Equal("Granos del Norte", lot.Supplier);
            Assert.Equal("5551234567", lot.SupplierPhone);
            Assert.Equal(BagStatus.Sealed, lot.Status);
        });
    }

    [Fact]
    public void Registra_gastos_sin_espacios_y_los_lista_del_mas_reciente()
    {
        var first = NewStore().RegisterExpense(new RegisterExpenseRequest(" Luz ", " Luz ", ExpenseArea.General, 300m, "  "));
        Thread.Sleep(5);
        NewStore().RegisterExpense(new RegisterExpenseRequest("Gasolina", "Insumos", ExpenseArea.Camioneta, 500m, " semana "));

        Assert.Equal("Luz", first.Concept);
        Assert.Null(first.Notes);
        var expenses = NewStore().GetExpenses();
        Assert.Equal(["Gasolina", "Luz"], expenses.Select(expense => expense.Concept));
        Assert.Equal(ExpenseArea.Camioneta, expenses[0].Area);
        Assert.Equal("semana", expenses[0].Notes);
    }

    [Fact]
    public void Resume_bultos_kilos_inversion_ingreso_ganancia_y_productos_por_surtir()
    {
        NewStore().AddProduct(new CreateProductRequest("Maiz quebrado", "Grano", 2));
        var bagId = NewStore().RegisterPurchase(Purchase(BagLine(bags: 2, purchaseCost: 600m)))[0].ProductId;
        NewStore().RegisterSaleBatch(Sale(Line(bagId, SaleUnit.Kg, 10)));

        var summary = NewStore().GetSummary();

        Assert.Equal(1, summary.BagsAvailable);
        Assert.Equal(30m, summary.OpenKgAvailable);
        Assert.Equal(600m, summary.Investment);
        Assert.Equal(110m, summary.Income);
        Assert.Equal(35m, summary.Profit);
        Assert.Equal(1, summary.LowStock);
    }

    [Theory]
    [InlineData("0409", "Aurelio")]
    [InlineData(" 2002 ", "Ana")]
    public void Autoriza_a_los_vendedores_conocidos(string sellerId, string name)
    {
        var authorization = NewStore().AuthorizeSeller(sellerId);

        Assert.True(authorization.Authorized);
        Assert.Equal(name, authorization.Seller?.Name);
    }

    [Theory]
    [InlineData("9999")]
    [InlineData("")]
    public void No_autoriza_a_vendedores_desconocidos(string sellerId)
    {
        var authorization = NewStore().AuthorizeSeller(sellerId);

        Assert.False(authorization.Authorized);
        Assert.Null(authorization.Seller);
    }

    [Fact]
    public void La_siembra_carga_tres_productos_con_bultos_y_no_se_repite()
    {
        using (var db = database.CreateContext())
        {
            PosStore.Seed(db);
            PosStore.Seed(db);
        }

        var inventory = NewStore().GetInventory();
        Assert.Equal(3, inventory.Count);
        Assert.Equal(5, inventory.Sum(item => item.SealedBags));
    }

    [Fact]
    public void La_siembra_no_toca_una_base_que_ya_tiene_productos()
    {
        NewStore().AddProduct(new CreateProductRequest("Propio", "General", 0));

        using (var db = database.CreateContext())
        {
            PosStore.Seed(db);
        }

        Assert.Equal("Propio", Assert.Single(NewStore().GetProducts()).Name);
    }
}
