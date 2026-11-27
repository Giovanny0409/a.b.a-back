using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(PosStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<InventoryItem>> GetInventory()
    {
        return Ok(store.GetInventory());
    }

    [HttpGet("lots")]
    public ActionResult<IReadOnlyList<BagLot>> GetLots()
    {
        return Ok(store.GetLots());
    }
}
