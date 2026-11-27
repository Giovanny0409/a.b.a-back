namespace BackendAba.Models;

public enum BagStatus
{
    Sealed,
    Open,
    Sold
}

public enum SaleUnit
{
    Bag,
    Kg,
    Piece
}

public enum PurchaseKind
{
    Bag,
    Unit
}

public enum ExpenseArea
{
    General,
    Guardadito,
    Camioneta
}

public sealed record Product(Guid Id, string Name, string Category, int MinBags);

public sealed record Supplier(Guid Id, string Name, string Phone);

public sealed record BagLot(
    Guid Id,
    Guid ProductId,
    string Supplier,
    string? SupplierName,
    string? SupplierPhone,
    DateTime PurchasedAt,
    decimal MarkedKg,
    decimal PurchaseCost,
    decimal SalePriceBag,
    decimal SalePriceKg,
    BagStatus Status,
    decimal RemainingKg);

public sealed record PurchaseRecord(
    Guid Id,
    DateTime PurchasedAt,
    PurchaseKind Kind,
    Guid ProductId,
    string ProductName,
    string? Category,
    string Supplier,
    string? SupplierName,
    string? SupplierPhone,
    int? Bags,
    decimal? Quantity,
    decimal? MarkedKg,
    decimal Investment);

public sealed record SaleLine(
    Guid Id,
    Guid ProductId,
    string ProductName,
    SaleUnit Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal Cost,
    decimal Total,
    decimal Profit);

public sealed record SaleRecord(
    Guid Id,
    DateTime SoldAt,
    string Customer,
    IReadOnlyList<SaleLine> Lines,
    decimal Total,
    decimal Cost,
    decimal Profit);

public sealed record Seller(
    string Id,
    string Name);

public sealed record SellerAuthorization(
    bool Authorized,
    Seller? Seller);

public sealed record ExpenseRecord(
    Guid Id,
    DateTime ExpenseAt,
    string Concept,
    string Category,
    ExpenseArea Area,
    decimal Amount,
    string? Notes);

public sealed record InventoryItem(
    Product Product,
    int SealedBags,
    decimal OpenKg,
    decimal QuantityTotal,
    bool NeedsRestock);

public sealed record DashboardSummary(
    int BagsAvailable,
    decimal OpenKgAvailable,
    decimal Investment,
    decimal Income,
    decimal Profit,
    int LowStock);
