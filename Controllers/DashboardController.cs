using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(PosStore store) : ControllerBase
{
    [HttpGet("summary")]
    public ActionResult<DashboardSummary> GetSummary()
    {
        return Ok(store.GetSummary());
    }
}
