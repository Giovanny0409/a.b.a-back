using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(PosStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<Product>> GetAll()
    {
        return Ok(store.GetProducts());
    }

    [HttpPost]
    public ActionResult<Product> Create(CreateProductRequest request)
    {
        var product = store.AddProduct(request);
        return CreatedAtAction(nameof(GetAll), new { id = product.Id }, product);
    }
}
