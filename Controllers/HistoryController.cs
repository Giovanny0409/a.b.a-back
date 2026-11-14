using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController(PosStore store) : ControllerBase
{
    [HttpGet("sales")]
    public ActionResult<IReadOnlyList<SaleRecord>> GetSales()
    {
        return Ok(store.GetSales());
    }

    [HttpGet("purchases")]
    public ActionResult<IReadOnlyList<PurchaseRecord>> GetPurchases()
    {
        return Ok(store.GetPurchases());
    }
}
