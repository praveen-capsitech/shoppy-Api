using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoppyApp.DTOs;
using ShoppyApp.Services;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Roles = "Customer")]
public sealed class CartController : ControllerBase
{
    private readonly CartService _service;

    public CartController(CartService service) => _service = service;

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public Task<CartResponse> Get() => _service.GetAsync(UserId);

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> Add(AddToCartRequest request)
    {
        try { return Ok(await _service.AddAsync(UserId, request)); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }

    [HttpPut("items/{productId}")]
    public async Task<ActionResult<CartResponse>> Update(string productId, UpdateCartItemRequest request)
    {
        try { return Ok(await _service.UpdateAsync(UserId, productId, request.Quantity)); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }

    [HttpDelete("items/{productId}")]
    public Task<CartResponse> Remove(string productId) =>
        _service.RemoveAsync(UserId, productId);

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _service.ClearAsync(UserId);
        return NoContent();
    }
}
