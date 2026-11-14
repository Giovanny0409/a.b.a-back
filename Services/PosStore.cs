using BackendAba.Data;
using BackendAba.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAba.Services;

public sealed class PosStore(AppDbContext db)
{
    private static readonly IReadOnlyDictionary<string, Seller> AuthorizedSellers = new Dictionary<string, Seller>(StringComparer.Ordinal)
    {
        ["0409"] = new("0409", "Aurelio"),
        ["2002"] = new("2002", "Ana")
    };

    public SellerAuthorization AuthorizeSeller(string sellerId)
    {
        var normalizedSellerId = NormalizeSellerId(sellerId);
        return AuthorizedSellers.TryGetValue(normalizedSellerId, out var seller)
            ? new SellerAuthorization(true, seller)
            : new SellerAuthorization(false, null);
    }

    public IReadOnlyList<Product> GetProducts()
    {
        return db.Products
            .OrderBy(product => product.Name)
            .Select(product => ToModel(product))
            .ToList();
    }

    public Product AddProduct(CreateProductRequest request)
    {
        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            MinBags = request.MinBags
        };

        db.Products.Add(product);
        db.SaveChanges();

        return ToModel(product);
    }

    public IReadOnlyList<BagLot> GetLots()
    {
        return db.BagLots
            .Include(lot => lot.Supplier)
            .OrderByDescending(lot => lot.PurchasedAt)
            .AsEnumerable()
            .Select(lot => ToModel(lot))
            .ToList();
    }

    public IReadOnlyList<InventoryItem> GetInventory()
    {
        return db.Products
            .Include(product => product.Lots)
            .Include(product => product.UnitLots)
            .OrderBy(product => product.Name)
            .AsEnumerable()
            .Select(product =>
            {
                var activeLots = product.Lots.Where(lot => lot.Status != BagStatus.Sold).ToList();
                var sealedBags = activeLots.Count(lot => lot.Status == BagStatus.Sealed);
                var openKg = activeLots.Where(lot => lot.Status == BagStatus.Open).Sum(lot => lot.RemainingKg);
                var quantityTotal = product.UnitLots.Sum(lot => lot.RemainingQuantity);

                return new InventoryItem(
                    ToModel(product),
                    sealedBags,
                    openKg,
                    quantityTotal,
                    sealedBags <= product.MinBags);
            }).ToList();
    }

    public DashboardSummary GetSummary()
    {
        var inventory = GetInventory();

        return new DashboardSummary(
            db.BagLots.Count(lot => lot.Status == BagStatus.Sealed),
            db.BagLots.Where(lot => lot.Status == BagStatus.Open).Sum(lot => lot.RemainingKg),
            db.Purchases.Sum(purchase => purchase.Investment),
            db.Sales.Sum(sale => sale.Total),
            db.Sales.Sum(sale => sale.Profit),
            inventory.Count(item => item.NeedsRestock));
    }

    public IReadOnlyList<PurchaseRecord> RegisterPurchase(RegisterPurchaseRequest request)
    {
        var purchasedAt = DateTime.UtcNow;
        var supplier = FindOrCreateSupplier(request.Supplier);
        var records = new List<PurchaseEntity>();

        foreach (var line in request.Lines)
        {
            var product = FindOrCreateProduct(line.ProductName, line.Category);

            if (line.Kind == PurchaseKind.Bag)
            {
                var bagCount = (int)line.Quantity;
                var markedKg = line.MarkedKg ?? 0;
                var costPerBag = bagCount > 0 ? line.PurchaseCost / bagCount : line.PurchaseCost;

                var newLots = Enumerable.Range(0, bagCount).Select(_ => new BagLotEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    SupplierId = supplier.Id,
                    PurchasedAt = purchasedAt,
                    MarkedKg = markedKg,
                    PurchaseCost = costPerBag,
                    SalePriceBag = line.SalePriceBag,
                    SalePriceKg = line.SalePriceKg,
                    Status = BagStatus.Sealed,
                    RemainingKg = markedKg
                }).ToList();

                db.BagLots.AddRange(newLots);

                records.Add(new PurchaseEntity
                {
                    Id = Guid.NewGuid(),
                    PurchasedAt = purchasedAt,
                    Kind = PurchaseKind.Bag,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Category = product.Category,
                    SupplierId = supplier.Id,
                    Bags = bagCount,
                    Quantity = null,
                    MarkedKg = markedKg,
                    Investment = line.PurchaseCost
                });
            }
            else
            {
                var unitLot = new UnitLotEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    SupplierId = supplier.Id,
                    PurchasedAt = purchasedAt,
                    Quantity = line.Quantity,
                    PurchaseCost = line.PurchaseCost,
                    SalePricePiece = line.SalePriceBag,
                    RemainingQuantity = line.Quantity
                };

                db.UnitLots.Add(unitLot);

                records.Add(new PurchaseEntity
                {
                    Id = Guid.NewGuid(),
                    PurchasedAt = purchasedAt,
                    Kind = PurchaseKind.Unit,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Category = product.Category,
                    SupplierId = supplier.Id,
                    Bags = null,
                    Quantity = line.Quantity,
                    MarkedKg = null,
                    Investment = line.PurchaseCost
                });
            }
        }

        db.Purchases.AddRange(records);
        db.SaveChanges();

        return records.Select(record => ToModel(record, supplier)).ToList();
    }

    public SaleLine? QuoteSale(Guid productId, SaleUnit unit, decimal quantity)
    {
        return QuoteSaleUnsafe(productId, unit, quantity);
    }

    public SaleRecord RegisterSale(RegisterSaleRequest request)
    {
        return RegisterSaleBatch(new RegisterSaleBatchRequest(
            request.Customer,
            request.SellerId,
            [new RegisterSaleLineRequest(request.ProductId, request.Unit, request.Quantity)]));
    }

    public SaleRecord RegisterSaleBatch(RegisterSaleBatchRequest request)
    {
        var seller = AuthorizeSeller(request.SellerId).Seller
            ?? throw new InvalidOperationException("Vendedor no autorizado para registrar ventas.");

        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("Agrega al menos un producto a la venta.");
        }

        using var transaction = db.Database.BeginTransaction();
        var soldAt = DateTime.UtcNow;
        var customer = string.IsNullOrWhiteSpace(request.Customer) ? "Publico general" : request.Customer.Trim();
        var saleLines = new List<SaleLineEntity>();

        foreach (var line in request.Lines)
        {
            var quoted = QuoteSaleUnsafe(line.ProductId, line.Unit, line.Quantity)
                ?? throw new InvalidOperationException("No hay inventario suficiente para esta venta.");

            ApplySaleToInventory(line.ProductId, line.Unit, line.Quantity);
            // Las búsquedas de lote filtran en la base: la siguiente línea debe ver este descuento.
            // Si una línea posterior no alcanza, la transacción se revierte completa.
            db.SaveChanges();

            saleLines.Add(new SaleLineEntity
            {
                Id = quoted.Id,
                ProductId = quoted.ProductId,
                ProductName = quoted.ProductName,
                Unit = quoted.Unit,
                Quantity = quoted.Quantity,
                UnitPrice = quoted.UnitPrice,
                Cost = quoted.Cost,
                Total = quoted.Total,
                Profit = quoted.Profit
            });
        }

        var sale = new SaleEntity
        {
            Id = Guid.NewGuid(),
            SoldAt = soldAt,
            Customer = $"{customer} | Vendedor: {seller.Name}",
            Total = saleLines.Sum(line => line.Total),
            Cost = saleLines.Sum(line => line.Cost),
            Profit = saleLines.Sum(line => line.Profit),
            Lines = saleLines
        };

        db.Sales.Add(sale);
        db.SaveChanges();
        transaction.Commit();

        return ToModel(sale);
    }

    private void ApplySaleToInventory(Guid productId, SaleUnit unit, decimal quantity)
    {
        if (unit == SaleUnit.Piece)
        {
            var unitLot = FindUnitLot(productId, quantity)
                ?? throw new InvalidOperationException("No se encontraron piezas disponibles.");

            unitLot.RemainingQuantity = Math.Max(0, unitLot.RemainingQuantity - quantity);
            return;
        }

        var lot = FindSaleLot(productId, unit, quantity)
            ?? throw new InvalidOperationException("No se encontro un bulto disponible.");

        if (unit == SaleUnit.Bag)
        {
            lot.Status = BagStatus.Sold;
            lot.RemainingKg = 0;
        }
        else
        {
            lot.RemainingKg = Math.Max(0, lot.RemainingKg - quantity);
            lot.Status = lot.RemainingKg <= 0.001m ? BagStatus.Sold : BagStatus.Open;
        }
    }

    public IReadOnlyList<PurchaseRecord> GetPurchases()
    {
        return db.Purchases
            .Include(purchase => purchase.Supplier)
            .OrderByDescending(purchase => purchase.PurchasedAt)
            .AsEnumerable()
            .Select(purchase => ToModel(purchase, purchase.Supplier))
            .ToList();
    }

    public IReadOnlyList<SaleRecord> GetSales()
    {
        return db.Sales
            .Include(sale => sale.Lines)
            .OrderByDescending(sale => sale.SoldAt)
            .AsEnumerable()
            .Select(sale => ToModel(sale))
            .ToList();
    }

    public IReadOnlyList<ExpenseRecord> GetExpenses()
    {
        return db.Expenses
            .OrderByDescending(expense => expense.ExpenseAt)
            .Select(expense => ToModel(expense))
            .ToList();
    }

    public ExpenseRecord RegisterExpense(RegisterExpenseRequest request)
    {
        var expense = new ExpenseEntity
        {
            Id = Guid.NewGuid(),
            ExpenseAt = DateTime.UtcNow,
            Concept = request.Concept.Trim(),
            Category = request.Category.Trim(),
            Area = request.Area,
            Amount = request.Amount,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.Expenses.Add(expense);
        db.SaveChanges();

        return ToModel(expense);
    }

    private SaleLine? QuoteSaleUnsafe(Guid productId, SaleUnit unit, decimal quantity)
    {
        if (quantity <= 0)
        {
            return null;
        }

        var product = db.Products.FirstOrDefault(item => item.Id == productId);
        if (product is null)
        {
            return null;
        }

        if (unit == SaleUnit.Piece)
        {
            var unitLot = FindUnitLot(productId, quantity);
            if (unitLot is null || unitLot.Quantity <= 0)
            {
                return null;
            }

            var costPerPiece = unitLot.PurchaseCost / unitLot.Quantity;
            var pieceCost = costPerPiece * quantity;
            var pieceTotal = unitLot.SalePricePiece * quantity;

            return new SaleLine(
                Guid.NewGuid(),
                product.Id,
                product.Name,
                unit,
                quantity,
                unitLot.SalePricePiece,
                Math.Round(pieceCost, 2),
                Math.Round(pieceTotal, 2),
                Math.Round(pieceTotal - pieceCost, 2));
        }

        var lot = FindSaleLot(productId, unit, quantity);

        if (lot is null || lot.MarkedKg <= 0)
        {
            return null;
        }

        var costPerKg = lot.PurchaseCost / lot.MarkedKg;
        var cost = unit == SaleUnit.Bag ? lot.PurchaseCost : costPerKg * quantity;
        var unitPrice = unit == SaleUnit.Bag ? lot.SalePriceBag : lot.SalePriceKg;
        var total = unitPrice * quantity;

        return new SaleLine(
            Guid.NewGuid(),
            product.Id,
            product.Name,
            unit,
            quantity,
            unitPrice,
            Math.Round(cost, 2),
            Math.Round(total, 2),
            Math.Round(total - cost, 2));
    }

    private BagLotEntity? FindSaleLot(Guid productId, SaleUnit unit, decimal quantity)
    {
        return unit == SaleUnit.Bag
            ? db.BagLots.FirstOrDefault(item => item.ProductId == productId && item.Status == BagStatus.Sealed)
            : db.BagLots.FirstOrDefault(item =>
                item.ProductId == productId &&
                (item.Status == BagStatus.Open || item.Status == BagStatus.Sealed) &&
                item.RemainingKg >= quantity);
    }

    private UnitLotEntity? FindUnitLot(Guid productId, decimal quantity)
    {
        return db.UnitLots.FirstOrDefault(item => item.ProductId == productId && item.RemainingQuantity >= quantity);
    }

    private SupplierEntity FindOrCreateSupplier(PurchaseSupplierRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? "Sin proveedor" : request.Name.Trim();
        var phone = request.Phone?.Trim() ?? string.Empty;

        var existing = db.Suppliers.FirstOrDefault(supplier => supplier.Name.ToLower() == name.ToLower());
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(phone) && existing.Phone != phone)
            {
                existing.Phone = phone;
            }

            return existing;
        }

        var supplier = new SupplierEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Phone = phone
        };

        db.Suppliers.Add(supplier);
        return supplier;
    }

    private ProductEntity FindOrCreateProduct(string productName, string? category)
    {
        var name = productName.Trim();
        var existing = db.Products.FirstOrDefault(product => product.Name.ToLower() == name.ToLower());
        if (existing is not null)
        {
            return existing;
        }

        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            MinBags = 0
        };

        db.Products.Add(product);
        return product;
    }

    private static string NormalizeSellerId(string? sellerId)
    {
        return string.IsNullOrWhiteSpace(sellerId) ? string.Empty : sellerId.Trim();
    }

    public static void Seed(AppDbContext db)
    {
        if (db.Products.Any())
        {
            return;
        }

        var maiz = new ProductEntity { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Maiz quebrado", Category = "Grano", MinBags = 4 };
        var engorda = new ProductEntity { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Alimento engorda", Category = "Balanceado", MinBags = 5 };
        var salvado = new ProductEntity { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Salvado de trigo", Category = "Forraje", MinBags = 3 };
        var now = DateTime.UtcNow;

        var supplier = new SupplierEntity { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Name = "Carga inicial", Phone = string.Empty };

        db.Products.AddRange(maiz, engorda, salvado);
        db.Suppliers.Add(supplier);
        db.BagLots.AddRange(
            DemoLot(maiz.Id, supplier.Id, now, 39.4m, 315, 390, 10.5m),
            DemoLot(maiz.Id, supplier.Id, now, 40.2m, 318, 395, 10.5m),
            DemoLot(engorda.Id, supplier.Id, now, 24.6m, 280, 345, 15),
            DemoLot(engorda.Id, supplier.Id, now, 25.1m, 282, 350, 15),
            DemoLot(salvado.Id, supplier.Id, now, 48.8m, 260, 330, 8.5m));
        db.SaveChanges();
    }

    private static BagLotEntity DemoLot(Guid productId, Guid supplierId, DateTime purchasedAt, decimal markedKg, decimal purchaseCost, decimal salePriceBag, decimal salePriceKg)
    {
        return new BagLotEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SupplierId = supplierId,
            PurchasedAt = purchasedAt,
            MarkedKg = markedKg,
            PurchaseCost = purchaseCost,
            SalePriceBag = salePriceBag,
            SalePriceKg = salePriceKg,
            Status = BagStatus.Sealed,
            RemainingKg = markedKg
        };
    }

    private static Product ToModel(ProductEntity product)
    {
        return new Product(product.Id, product.Name, product.Category, product.MinBags);
    }

    private static BagLot ToModel(BagLotEntity lot)
    {
        return new BagLot(
            lot.Id,
            lot.ProductId,
            lot.Supplier?.Name ?? string.Empty,
            lot.Supplier?.Name,
            lot.Supplier?.Phone,
            lot.PurchasedAt,
            lot.MarkedKg,
            lot.PurchaseCost,
            lot.SalePriceBag,
            lot.SalePriceKg,
            lot.Status,
            lot.RemainingKg);
    }

    private static PurchaseRecord ToModel(PurchaseEntity purchase, SupplierEntity? supplier)
    {
        return new PurchaseRecord(
            purchase.Id,
            purchase.PurchasedAt,
            purchase.Kind,
            purchase.ProductId,
            purchase.ProductName,
            purchase.Category,
            supplier?.Name ?? string.Empty,
            supplier?.Name,
            supplier?.Phone,
            purchase.Bags,
            purchase.Quantity,
            purchase.MarkedKg,
            purchase.Investment);
    }

    private static SaleRecord ToModel(SaleEntity sale)
    {
        var lines = sale.Lines.Select(line => new SaleLine(line.Id, line.ProductId, line.ProductName, line.Unit, line.Quantity, line.UnitPrice, line.Cost, line.Total, line.Profit)).ToList();
        return new SaleRecord(sale.Id, sale.SoldAt, sale.Customer, lines, sale.Total, sale.Cost, sale.Profit);
    }

    private static ExpenseRecord ToModel(ExpenseEntity expense)
    {
        return new ExpenseRecord(expense.Id, expense.ExpenseAt, expense.Concept, expense.Category, expense.Area, expense.Amount, expense.Notes);
    }
}
