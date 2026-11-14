using BackendAba.Models;

namespace BackendAba.Tests.Infrastructure;

/// <summary>Solicitudes válidas con valores por omisión, para que cada prueba cambie solo lo que le importa.</summary>
public static class Requests
{
    public const string AuthorizedSeller = "0409";

    public static PurchaseLineRequest BagLine(
        string productName = "Maiz quebrado",
        int bags = 2,
        decimal markedKg = 40m,
        decimal purchaseCost = 600m,
        decimal salePriceBag = 400m,
        decimal salePriceKg = 11m,
        string? category = "Grano") =>
        new(PurchaseKind.Bag, productName, category, bags, markedKg, purchaseCost, salePriceBag, salePriceKg);

    public static PurchaseLineRequest UnitLine(
        string productName = "Correa",
        decimal quantity = 10m,
        decimal purchaseCost = 500m,
        decimal salePricePiece = 80m,
        string? category = "Accesorio") =>
        new(PurchaseKind.Unit, productName, category, quantity, null, purchaseCost, salePricePiece, salePricePiece);

    public static RegisterPurchaseRequest Purchase(params PurchaseLineRequest[] lines) =>
        Purchase("Granos del Norte", "5551234567", lines);

    public static RegisterPurchaseRequest Purchase(string supplier, string? phone, params PurchaseLineRequest[] lines) =>
        new(new PurchaseSupplierRequest(supplier, phone), lines);

    public static RegisterSaleBatchRequest Sale(params RegisterSaleLineRequest[] lines) =>
        new("Juan", AuthorizedSeller, lines);

    public static RegisterSaleLineRequest Line(Guid productId, SaleUnit unit, decimal quantity) =>
        new(productId, unit, quantity);
}
