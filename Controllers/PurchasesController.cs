using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/purchases")]
public sealed class PurchasesController(PosStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<PurchaseRecord>> GetAll()
    {
        return Ok(store.GetPurchases());
    }

    [HttpPost]
    public ActionResult<IReadOnlyList<PurchaseRecord>> Register(RegisterPurchaseRequest request)
    {
        try
        {
            return Ok(store.RegisterPurchase(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
