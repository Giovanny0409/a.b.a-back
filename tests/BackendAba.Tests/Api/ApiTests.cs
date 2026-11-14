using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BackendAba.Tests.Infrastructure;

namespace BackendAba.Tests.Api;

/// <summary>
/// Prueba la API por HTTP: rutas, códigos de respuesta y nombres de propiedades en camelCase, que es
/// el contrato que consumen core/pos.models.ts y core/pos-api.service.ts del front.
/// </summary>
public sealed class ApiTests : IDisposable
{
    // Productos de la siembra (PosStore.Seed): Maiz quebrado con dos bultos de precios distintos y
    // Salvado de trigo con un solo bulto de 330. Con dos bultos, cuál se vende es arbitrario (P-13), así
    // que las ventas por bulto usan el de un solo bulto.
    private const string SeededProductId = "10000000-0000-0000-0000-000000000001";
    private const string SingleBagProductId = "10000000-0000-0000-0000-000000000003";

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public ApiTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private async Task<JsonElement> GetJson(string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object PurchaseBody(string productName = "Avena", string kind = "bag") => new
    {
        supplier = new { name = "Granos del Norte", phone = "555" },
        lines = new[]
        {
            new
            {
                kind,
                productName,
                category = "Grano",
                quantity = 2,
                markedKg = (decimal?)40,
                purchaseCost = 600m,
                salePriceBag = 400m,
                salePriceKg = 11m
            }
        }
    };

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/inventory")]
    [InlineData("/api/inventory/lots")]
    [InlineData("/api/purchases")]
    [InlineData("/api/sales")]
    [InlineData("/api/expenses")]
    [InlineData("/api/history/sales")]
    [InlineData("/api/history/purchases")]
    public async Task Las_consultas_responden_una_lista(string url)
    {
        var json = await GetJson(url);

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
    }

    [Fact]
    public async Task El_inventario_sembrado_usa_los_nombres_que_espera_el_front()
    {
        var item = (await GetJson("/api/inventory")).EnumerateArray().First();

        Assert.True(item.TryGetProperty("product", out var product));
        Assert.True(product.TryGetProperty("minBags", out _));
        foreach (var name in new[] { "sealedBags", "openKg", "quantityTotal", "needsRestock" })
        {
            Assert.True(item.TryGetProperty(name, out _), $"Falta {name}");
        }
    }

    [Fact]
    public async Task Los_lotes_mandan_el_estado_en_camelCase()
    {
        var lot = (await GetJson("/api/inventory/lots")).EnumerateArray().First();

        Assert.Equal("sealed", lot.GetProperty("status").GetString());
        Assert.True(lot.TryGetProperty("remainingKg", out _));
    }

    [Fact]
    public async Task Registrar_una_compra_responde_los_registros_en_camelCase()
    {
        var response = await client.PostAsJsonAsync("/api/purchases", PurchaseBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single();
        Assert.Equal("bag", record.GetProperty("kind").GetString());
        Assert.Equal("Avena", record.GetProperty("productName").GetString());
        Assert.Equal("Granos del Norte", record.GetProperty("supplierName").GetString());
        Assert.Equal("555", record.GetProperty("supplierPhone").GetString());
        Assert.Equal(600m, record.GetProperty("investment").GetDecimal());
        Assert.Equal(2, record.GetProperty("bags").GetInt32());
    }

    [Fact]
    public async Task Una_compra_sin_lineas_responde_400()
    {
        var response = await client.PostAsJsonAsync("/api/purchases", new
        {
            supplier = new { name = "Granos del Norte", phone = "" },
            lines = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cotiza_una_venta_y_responde_404_sin_inventario()
    {
        var quote = await GetJson($"/api/sales/quote?productId={SeededProductId}&unit=kg&quantity=2");
        Assert.Equal("kg", quote.GetProperty("unit").GetString());
        Assert.Equal(21m, quote.GetProperty("total").GetDecimal());

        var response = await client.GetAsync($"/api/sales/quote?productId={SeededProductId}&unit=bag&quantity=0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("No hay inventario suficiente.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Registrar_una_venta_en_lote_baja_el_inventario()
    {
        var response = await client.PostAsJsonAsync("/api/sales/batch", new
        {
            customer = "Juan",
            sellerId = "0409",
            lines = new[] { new { productId = SingleBagProductId, unit = "bag", quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(330m, sale.GetProperty("total").GetDecimal());
        Assert.Equal("bag", sale.GetProperty("lines")[0].GetProperty("unit").GetString());

        var item = (await GetJson("/api/inventory")).EnumerateArray()
            .Single(entry => entry.GetProperty("product").GetProperty("id").GetString() == SingleBagProductId);
        Assert.Equal(0, item.GetProperty("sealedBags").GetInt32());
    }

    [Fact]
    public async Task Una_venta_sin_inventario_responde_400_con_mensaje_y_no_se_registra()
    {
        var response = await client.PostAsJsonAsync("/api/sales/batch", new
        {
            customer = "Juan",
            sellerId = "0409",
            lines = new[] { new { productId = SeededProductId, unit = "piece", quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("No hay inventario suficiente para esta venta.", body.GetProperty("message").GetString());
        Assert.Equal(0, (await GetJson("/api/sales")).GetArrayLength());
    }

    [Fact]
    public async Task Dos_lineas_del_unico_bulto_responden_400_y_no_se_registra_la_venta()
    {
        var response = await client.PostAsJsonAsync("/api/sales/batch", new
        {
            customer = "Juan",
            sellerId = "0409",
            lines = new[]
            {
                new { productId = SingleBagProductId, unit = "bag", quantity = 1 },
                new { productId = SingleBagProductId, unit = "bag", quantity = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("No hay inventario suficiente para esta venta.", body.GetProperty("message").GetString());
        Assert.Equal(0, (await GetJson("/api/sales")).GetArrayLength());
        var item = (await GetJson("/api/inventory")).EnumerateArray()
            .Single(entry => entry.GetProperty("product").GetProperty("id").GetString() == SingleBagProductId);
        Assert.Equal(1, item.GetProperty("sealedBags").GetInt32());
    }

    [Fact]
    public async Task Una_venta_de_un_vendedor_no_autorizado_responde_400()
    {
        var response = await client.PostAsJsonAsync("/api/sales/batch", new
        {
            customer = "Juan",
            sellerId = "9999",
            lines = new[] { new { productId = SeededProductId, unit = "bag", quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Autoriza_al_vendedor_conocido_y_responde_401_al_desconocido()
    {
        var ok = await client.PostAsJsonAsync("/api/sellers/authorize", new { sellerId = "0409" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var authorization = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(authorization.GetProperty("authorized").GetBoolean());
        Assert.Equal("Aurelio", authorization.GetProperty("seller").GetProperty("name").GetString());

        var denied = await client.PostAsJsonAsync("/api/sellers/authorize", new { sellerId = "9999" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Registrar_un_gasto_acepta_y_responde_el_area_en_camelCase()
    {
        var response = await client.PostAsJsonAsync("/api/expenses", new
        {
            concept = "Gasolina",
            category = "Insumos",
            area = "camioneta",
            amount = 500m,
            notes = "semana"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expense = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("camioneta", expense.GetProperty("area").GetString());
        Assert.True(expense.TryGetProperty("expenseAt", out _));
        Assert.Equal(1, (await GetJson("/api/expenses")).GetArrayLength());
    }

    [Fact]
    public async Task Un_gasto_sin_monto_responde_400()
    {
        var response = await client.PostAsJsonAsync("/api/expenses", new
        {
            concept = "Gasolina",
            category = "Insumos",
            area = "general",
            amount = 0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task El_resumen_del_tablero_responde()
    {
        var summary = await GetJson("/api/dashboard/summary");

        Assert.Equal(5, summary.GetProperty("bagsAvailable").GetInt32());
    }
}
