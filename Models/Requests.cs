using System.ComponentModel.DataAnnotations;

namespace BackendAba.Models;

public sealed record CreateProductRequest(
    [Required] string Name,
    [Required] string Category,
    [Range(0, 999)] int MinBags);

public sealed record PurchaseSupplierRequest(
    [Required] string Name,
    string? Phone);

public sealed record PurchaseLineRequest(
    PurchaseKind Kind,
    [Required] string ProductName,
    string? Category,
    [Range(1, 999999)] decimal Quantity,
    decimal? MarkedKg,
    [Range(0, 999999)] decimal PurchaseCost,
    [Range(0, 999999)] decimal SalePriceBag,
    decimal SalePriceKg);

public sealed record RegisterPurchaseRequest(
    [Required] PurchaseSupplierRequest Supplier,
    [Required, MinLength(1)] IReadOnlyList<PurchaseLineRequest> Lines);

public sealed record RegisterSaleRequest(
    string? Customer,
    [Required] string SellerId,
    [Required] Guid ProductId,
    SaleUnit Unit,
    [Range(0.01, 999999)] decimal Quantity);

public sealed record RegisterSaleLineRequest(
    [Required] Guid ProductId,
    SaleUnit Unit,
    [Range(0.01, 999999)] decimal Quantity);

public sealed record RegisterSaleBatchRequest(
    string? Customer,
    [Required] string SellerId,
    [Required, MinLength(1)] IReadOnlyList<RegisterSaleLineRequest> Lines);

public sealed record AuthorizeSellerRequest(
    [Required] string SellerId);

public sealed record RegisterExpenseRequest(
    [Required] string Concept,
    [Required] string Category,
    ExpenseArea Area,
    [Range(0.01, 999999)] decimal Amount,
    string? Notes);
