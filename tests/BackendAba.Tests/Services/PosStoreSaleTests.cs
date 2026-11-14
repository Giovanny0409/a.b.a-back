using BackendAba.Models;
using BackendAba.Services;
using BackendAba.Tests.Infrastructure;
using static BackendAba.Tests.Infrastructure.Requests;

namespace BackendAba.Tests.Services;

public sealed class PosStoreSaleTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    private PosStore NewStore() => new(database.CreateContext());

    /// <summary>Un bulto de 40 kg que costó 300 y se vende a 400 el bulto o a 11 el kilo.</summary>
    private Guid BuyBags(int bags = 1, decimal markedKg = 40m) =>
        NewStore().RegisterPurchase(Purchase(BagLine(bags: bags, markedKg: markedKg, purchaseCost: 300m * bags)))[0].ProductId;

    /// <summary>10 piezas que costaron 500 (50 c/u) y se venden a 80.</summary>
    private Guid BuyPieces() => NewStore().RegisterPurchase(Purchase(UnitLine()))[0].ProductId;

    [Fact]
    public void Cotiza_un_bulto_con_su_precio_y_su_costo()
    {
        var productId = BuyBags();

        var quote = NewStore().QuoteSale(productId, SaleUnit.Bag, 1);

        Assert.NotNull(quote);
        Assert.Equal(400m, quote.UnitPrice);
        Assert.Equal(300m, quote.Cost);
        Assert.Equal(400m, quote.Total);
        Assert.Equal(100m, quote.Profit);
    }

    [Fact]
    public void Cotiza_kilos_con_el_costo_por_kilo_del_lote()
    {
        var productId = BuyBags();

        var quote = NewStore().QuoteSale(productId, SaleUnit.Kg, 2.5m);

        Assert.NotNull(quote);
        Assert.Equal(11m, quote.UnitPrice);
        Assert.Equal(18.75m, quote.Cost);
        Assert.Equal(27.5m, quote.Total);
        Assert.Equal(8.75m, quote.Profit);
    }

    [Fact]
    public void Cotiza_piezas_con_el_costo_por_pieza_del_lote()
    {
        var productId = BuyPieces();

        var quote = NewStore().QuoteSale(productId, SaleUnit.Piece, 3);

        Assert.NotNull(quote);
        Assert.Equal(80m, quote.UnitPrice);
        Assert.Equal(150m, quote.Cost);
        Assert.Equal(240m, quote.Total);
        Assert.Equal(90m, quote.Profit);
    }

    [Theory]
    [InlineData(SaleUnit.Bag, 0)]
    [InlineData(SaleUnit.Kg, -1)]
    [InlineData(SaleUnit.Kg, 41)]
    [InlineData(SaleUnit.Piece, 1)]
    public void No_cotiza_sin_cantidad_o_sin_inventario_suficiente(SaleUnit unit, decimal quantity)
    {
        var productId = BuyBags();

        Assert.Null(NewStore().QuoteSale(productId, unit, quantity));
    }

    [Fact]
    public void No_cotiza_un_producto_que_no_existe()
    {
        Assert.Null(NewStore().QuoteSale(Guid.NewGuid(), SaleUnit.Bag, 1));
    }

    [Fact]
    public void Vender_un_bulto_lo_marca_vendido_y_baja_el_inventario()
    {
        var productId = BuyBags(bags: 2);

        var sale = NewStore().RegisterSaleBatch(Sale(Line(productId, SaleUnit.Bag, 1)));

        Assert.Equal(400m, sale.Total);
        Assert.Equal(300m, sale.Cost);
        Assert.Equal(100m, sale.Profit);
        Assert.Equal("Juan | Vendedor: Aurelio", sale.Customer);
        var item = Assert.Single(NewStore().GetInventory());
        Assert.Equal(1, item.SealedBags);
        Assert.Equal(0m, item.OpenKg);
    }

    [Fact]
    public void Vender_kilos_abre_el_bulto_y_deja_los_kilos_restantes()
    {
        var productId = BuyBags();

        NewStore().RegisterSaleBatch(Sale(Line(productId, SaleUnit.Kg, 10)));

        var item = Assert.Single(NewStore().GetInventory());
        Assert.Equal(0, item.SealedBags);
        Assert.Equal(30m, item.OpenKg);
        using var db = database.CreateContext();
        Assert.Equal(BagStatus.Open, Assert.Single(db.BagLots).Status);
    }

    [Fact]
    public void Vender_todos_los_kilos_de_un_bulto_abierto_lo_marca_vendido()
    {
        var productId = BuyBags();
        NewStore().RegisterSaleBatch(Sale(Line(productId, SaleUnit.Kg, 30)));

        NewStore().RegisterSaleBatch(Sale(Line(productId, SaleUnit.Kg, 10)));

        using var db = database.CreateContext();
        var lot = Assert.Single(db.BagLots);
        Assert.Equal(BagStatus.Sold, lot.Status);
        Assert.Equal(0m, lot.RemainingKg);
    }

    [Fact]
    public void Compara_los_kilos_como_numero_y_no_como_texto()
    {
        // Con 9.5 kg restantes no se pueden vender 10 kg; comparados como texto, "9.5" >= "10" sería verdadero.
        var productId = BuyBags(markedKg: 9.5m);

        Assert.Null(NewStore().QuoteSale(productId, SaleUnit.Kg, 10));
        Assert.NotNull(NewStore().QuoteSale(productId, SaleUnit.Kg, 9.5m));
    }

    [Fact]
    public void Vender_piezas_baja_el_inventario_de_piezas()
    {
        var productId = BuyPieces();

        var sale = NewStore().RegisterSaleBatch(Sale(Line(productId, SaleUnit.Piece, 3)));

        Assert.Equal(240m, sale.Total);
        Assert.Equal(7m, Assert.Single(NewStore().GetInventory()).QuantityTotal);
    }

    [Fact]
    public void Una_venta_de_varias_lineas_suma_total_costo_y_ganancia()
    {
        var bagId = BuyBags(bags: 2);
        var pieceId = BuyPieces();

        var sale = NewStore().RegisterSaleBatch(Sale(
            Line(bagId, SaleUnit.Bag, 1),
            Line(pieceId, SaleUnit.Piece, 2)));

        Assert.Equal(2, sale.Lines.Count);
        Assert.Equal(560m, sale.Total);
        Assert.Equal(400m, sale.Cost);
        Assert.Equal(160m, sale.Profit);
    }

    [Fact]
    public void Sin_cliente_registra_Publico_general()
    {
        var productId = BuyBags();

        var sale = NewStore().RegisterSaleBatch(new RegisterSaleBatchRequest("  ", AuthorizedSeller, [Line(productId, SaleUnit.Bag, 1)]));

        Assert.Equal("Publico general | Vendedor: Aurelio", sale.Customer);
    }

    [Fact]
    public void Rechaza_la_venta_de_un_vendedor_no_autorizado()
    {
        var productId = BuyBags();

        var error = Assert.Throws<InvalidOperationException>(() =>
            NewStore().RegisterSaleBatch(new RegisterSaleBatchRequest("Juan", "9999", [Line(productId, SaleUnit.Bag, 1)])));

        Assert.Equal("Vendedor no autorizado para registrar ventas.", error.Message);
    }

    [Fact]
    public void Rechaza_una_venta_sin_lineas()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NewStore().RegisterSaleBatch(Sale()));

        Assert.Equal("Agrega al menos un producto a la venta.", error.Message);
    }

    [Fact]
    public void Si_una_linea_no_tiene_inventario_no_registra_nada()
    {
        var bagId = BuyBags();
        var otherId = NewStore().RegisterPurchase(Purchase(BagLine(productName: "Sin bultos", bags: 0)))[0].ProductId;

        Assert.Throws<InvalidOperationException>(() => NewStore().RegisterSaleBatch(Sale(
            Line(bagId, SaleUnit.Bag, 1),
            Line(otherId, SaleUnit.Bag, 1))));

        using var db = database.CreateContext();
        Assert.Empty(db.Sales);
        Assert.Equal(BagStatus.Sealed, Assert.Single(db.BagLots).Status);
    }

    [Fact]
    public void Dos_lineas_del_mismo_producto_no_venden_mas_de_lo_que_hay()
    {
        var bagId = BuyBags();

        var error = Assert.Throws<InvalidOperationException>(() => NewStore().RegisterSaleBatch(Sale(
            Line(bagId, SaleUnit.Bag, 1),
            Line(bagId, SaleUnit.Bag, 1))));

        Assert.Equal("No hay inventario suficiente para esta venta.", error.Message);
        using var db = database.CreateContext();
        Assert.Empty(db.Sales);
        Assert.Equal(BagStatus.Sealed, Assert.Single(db.BagLots).Status);
    }

    [Fact]
    public void Dos_lineas_de_kilos_no_venden_mas_kilos_de_los_que_tiene_el_bulto()
    {
        var bagId = BuyBags();

        Assert.Throws<InvalidOperationException>(() => NewStore().RegisterSaleBatch(Sale(
            Line(bagId, SaleUnit.Kg, 30),
            Line(bagId, SaleUnit.Kg, 30))));

        using var db = database.CreateContext();
        Assert.Empty(db.Sales);
        var lot = Assert.Single(db.BagLots);
        Assert.Equal(BagStatus.Sealed, lot.Status);
        Assert.Equal(40m, lot.RemainingKg);
    }

    [Fact]
    public void Dos_lineas_de_piezas_no_venden_mas_piezas_de_las_que_hay()
    {
        var pieceId = BuyPieces();

        Assert.Throws<InvalidOperationException>(() => NewStore().RegisterSaleBatch(Sale(
            Line(pieceId, SaleUnit.Piece, 6),
            Line(pieceId, SaleUnit.Piece, 6))));

        using var db = database.CreateContext();
        Assert.Empty(db.Sales);
        Assert.Equal(10m, Assert.Single(db.UnitLots).RemainingQuantity);
    }

    [Fact]
    public void Dos_lineas_del_mismo_producto_que_si_alcanzan_venden_un_bulto_cada_una()
    {
        var bagId = BuyBags(bags: 2);

        var sale = NewStore().RegisterSaleBatch(Sale(
            Line(bagId, SaleUnit.Bag, 1),
            Line(bagId, SaleUnit.Bag, 1)));

        Assert.Equal(2, sale.Lines.Count);
        Assert.Equal(800m, sale.Total);
        using var db = database.CreateContext();
        Assert.All(db.BagLots, lot => Assert.Equal(BagStatus.Sold, lot.Status));
        Assert.Equal(0, Assert.Single(NewStore().GetInventory()).SealedBags);
    }

    [Fact]
    public void La_venta_de_una_sola_linea_se_registra_igual_que_en_lote()
    {
        var productId = BuyBags();

        var sale = NewStore().RegisterSale(new RegisterSaleRequest(null, AuthorizedSeller, productId, SaleUnit.Bag, 1));

        Assert.Single(sale.Lines);
        Assert.Single(NewStore().GetSales());
    }
}
