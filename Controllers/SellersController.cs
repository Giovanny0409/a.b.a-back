using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/sellers")]
public sealed class SellersController(PosStore store) : ControllerBase
{
    [HttpPost("authorize")]
    public ActionResult<SellerAuthorization> Authorize(AuthorizeSellerRequest request)
    {
        var authorization = store.AuthorizeSeller(request.SellerId);
        return authorization.Authorized
            ? Ok(authorization)
            : Unauthorized(authorization);
    }
}
