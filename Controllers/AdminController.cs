using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
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
        var result = await _db.Users.UpdateOneAsync(x => x.Id == id, Builders<User>.Update.Set(x => x.Role, request.Role));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ChangeStatus(string id, [FromBody] ChangeStatusRequest request)
    {
        var result = await _db.Users.UpdateOneAsync(x => x.Id == id, Builders<User>.Update.Set(x => x.IsActive, request.IsActive));
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }
}
public record ChangeRoleRequest(string Role);
public record ChangeStatusRequest(bool IsActive);
