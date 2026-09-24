using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ShoppyApp.Binding;
using ShoppyApp.DTOs;
using ShoppyApp.Models;
using ShoppyApp.Services;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _products;

    public ProductsController(ProductService products) => _products = products;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll()
    {
        return Ok(await _products.GetAllAsync());
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpGet("managed")]
    public async Task<ActionResult<IEnumerable<Product>>> GetManaged()
    {
        return Ok(await _products.GetManagedAsync(CurrentUserId, User.IsInRole("Admin")));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDetailsResponse>> Get(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id)
    {
        var product = await _products.GetDetailsAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
    {
        try
        {
            var product = await _products.CreateAsync(CurrentUserId, request);
            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> Update(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id,
        [FromBody] UpdateProductRequest request)
    {
        try
        {
            var product = await _products.UpdateAsync(
                id, CurrentUserId, User.IsInRole("Admin"), request);

            return product is null ? NotFound() : Ok(product);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id)
    {
        try
        {
            var deleted = await _products.DeleteAsync(id, CurrentUserId, User.IsInRole("Admin"));
            return deleted ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
