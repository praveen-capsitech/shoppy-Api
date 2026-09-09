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
    private readonly MongoDbContext _db;
    public AdminController(MongoDbContext db) => _db = db;

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<object>>> Users()
    {
        var users = await _db.Users.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(users.Select(x => new { x.Id, x.Name, x.Email, x.Role, x.IsActive, x.CreatedAt }));
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleRequest request)
    {
        var valid = new[] { UserRoles.Customer, UserRoles.Manager, UserRoles.Admin };
        if (!valid.Contains(request.Role)) return BadRequest(new { message = "Invalid role." });

        var target = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (target is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (target.Id == currentUserId && target.Role == UserRoles.Admin && request.Role != UserRoles.Admin)
            return Conflict(new { message = "You cannot remove your own administrator role." });

        if (target.Role == UserRoles.Admin && request.Role != UserRoles.Admin && target.IsActive && await IsLastActiveAdminAsync(target.Id))
            return Conflict(new { message = "At least one active administrator is required." });

        var result = await _db.Users.UpdateOneAsync(
            x => x.Id == id && x.Role == target.Role,
            Builders<User>.Update.Set(x => x.Role, request.Role));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ChangeStatus(string id, [FromBody] ChangeStatusRequest request)
    {
        var target = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (target is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (target.Id == currentUserId && !request.IsActive)
            return Conflict(new { message = "You cannot deactivate your own account." });

        if (target.Role == UserRoles.Admin && target.IsActive && !request.IsActive && await IsLastActiveAdminAsync(target.Id))
            return Conflict(new { message = "At least one active administrator is required." });

        var result = await _db.Users.UpdateOneAsync(
            x => x.Id == id && x.IsActive == target.IsActive,
            Builders<User>.Update.Set(x => x.IsActive, request.IsActive));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    private async Task<bool> IsLastActiveAdminAsync(string excludedId)
    {
        var activeAdminCount = await _db.Users.CountDocumentsAsync(
            x => x.Role == UserRoles.Admin && x.IsActive && x.Id != excludedId);
        return activeAdminCount == 0;
    }
}
public record ChangeRoleRequest(string Role);
public record ChangeStatusRequest(bool IsActive);
