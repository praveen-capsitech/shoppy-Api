using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoppyApp.Binding;
using ShoppyApp.DTOs;
using ShoppyApp.Models;
using ShoppyApp.Services;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly OrderService _service;

    public OrderController(OrderService service) => _service = service;

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    [HttpPost("cod")]
    [Authorize(Roles = "Customer,Manager,Admin")]
    public async Task<ActionResult<OrderResponse>> CreateCod([FromBody] CreateCodOrderRequest request)
    {
        try { return Ok(await _service.CreateCodAsync(UserId, request)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Customer,Manager,Admin")]
    public Task<IReadOnlyList<OrderResponse>> Mine() => _service.GetMineAsync(UserId);

    [HttpGet("mine/{id}")]
    [Authorize(Roles = "Customer,Manager,Admin")]
    public async Task<ActionResult<OrderResponse>> MineById(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id)
    {
        var order = await _service.GetMineByIdAsync(UserId, id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("mine/{id}/cancel")]
    [Authorize(Roles = "Customer,Manager,Admin")]
    public async Task<ActionResult<OrderResponse>> Cancel(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id)
    {
        try
        {
            var order = await _service.CancelMineAsync(UserId, id);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }

    [HttpGet("manage")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> Manage()
    {
        try
        {
            var orders = await _service.GetManageAsync(UserId, User.IsInRole("Admin"));
            return Ok(orders);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPatch("manage/{id}/status")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string id,
        [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var order = await _service.UpdateStatusAsync(id, request.Status, UserId, User.IsInRole("Admin"));
            return order is null ? NotFound() : Ok(order);
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }
}
