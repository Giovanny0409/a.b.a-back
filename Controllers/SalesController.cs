using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(PosStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<SaleRecord>> GetAll()
    {
        return Ok(store.GetSales());
    }

    [HttpGet("quote")]
    public ActionResult<SaleLine> Quote([FromQuery] Guid productId, [FromQuery] SaleUnit unit, [FromQuery] decimal quantity)
    {
        try
        {
            var quote = store.QuoteSale(productId, unit, quantity);
            return quote is null ? NotFound(new { message = "No hay inventario suficiente." }) : Ok(quote);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public ActionResult<SaleRecord> Register(RegisterSaleRequest request)
    {
        try
        {
            return Ok(store.RegisterSale(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("batch")]
    public ActionResult<SaleRecord> RegisterBatch(RegisterSaleBatchRequest request)
    {
        try
        {
            return Ok(store.RegisterSaleBatch(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
