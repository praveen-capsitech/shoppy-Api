using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;
using ShoppyApp.Data;
using ShoppyApp.Models;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly MongoDbContext _database;

    public AdminController(MongoDbContext database) => _database = database;

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<object>>> Users()
    {
        var users = await _database.Users.Find(_ => true)
            .SortByDescending(user => user.CreatedAt)
            .ToListAsync();

        var response = users.Select(user => new
        {
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt
        });

        return Ok(response);
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleRequest request)
    {
        if (!IsValidRole(request.Role))
            return BadRequest(new { message = "Invalid role." });

        var target = await _database.Users.Find(user => user.Id == id).FirstOrDefaultAsync();
        if (target is null) return NotFound();

        if (target.Id == CurrentUserId &&
            target.Role == UserRoles.Admin &&
            request.Role != UserRoles.Admin)
            return Conflict(new { message = "You cannot remove your own administrator role." });

        if (target.Role == UserRoles.Admin &&
            request.Role != UserRoles.Admin &&
            target.IsActive &&
            await IsLastActiveAdminAsync(target.Id))
            return Conflict(new { message = "At least one active administrator is required." });

        var result = await _database.Users.UpdateOneAsync(
            user => user.Id == id && user.Role == target.Role,
            Builders<User>.Update.Set(user => user.Role, request.Role));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ChangeStatus(string id, [FromBody] ChangeStatusRequest request)
    {
        var target = await _database.Users.Find(user => user.Id == id).FirstOrDefaultAsync();
        if (target is null) return NotFound();

        if (target.Id == CurrentUserId && !request.IsActive)
            return Conflict(new { message = "You cannot deactivate your own account." });

        if (target.Role == UserRoles.Admin &&
            target.IsActive &&
            !request.IsActive &&
            await IsLastActiveAdminAsync(target.Id))
            return Conflict(new { message = "At least one active administrator is required." });

        var result = await _database.Users.UpdateOneAsync(
            user => user.Id == id && user.IsActive == target.IsActive,
            Builders<User>.Update.Set(user => user.IsActive, request.IsActive));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    private async Task<bool> IsLastActiveAdminAsync(string excludedId)
    {
        var activeAdminCount = await _database.Users.CountDocumentsAsync(
            user => user.Role == UserRoles.Admin && user.IsActive && user.Id != excludedId);
        return activeAdminCount == 0;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static bool IsValidRole(string role)
    {
        return role is UserRoles.Customer or UserRoles.Manager or UserRoles.Admin;
    }
}
public record ChangeRoleRequest(string Role);
public record ChangeStatusRequest(bool IsActive);
