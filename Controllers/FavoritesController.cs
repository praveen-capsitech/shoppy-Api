using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShoppyApp.Binding;
using ShoppyApp.Data;
using ShoppyApp.Models;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly MongoDbContext _database;

    public FavoritesController(MongoDbContext database) => _database = database;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> Get()
    {
        var user = await _database.Users.Find(item => item.Id == CurrentUserId).FirstOrDefaultAsync();
        return user is null ? Unauthorized() : Ok(user.FavoriteProductIds);
    }

    [HttpPost("{productId}")]
    public async Task<IActionResult> Add(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string productId)
    {
        if (!await _database.Products.Find(product => product.Id == productId).AnyAsync())
            return NotFound(new { message = "Product not found." });

        var result = await _database.Users.UpdateOneAsync(
            user => user.Id == CurrentUserId,
            Builders<User>.Update.AddToSet(user => user.FavoriteProductIds, productId));

        return result.MatchedCount == 0 ? Unauthorized() : NoContent();
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> Remove(
        [FromRoute, ModelBinder(BinderType = typeof(MongoIdModelBinder))] string productId)
    {
        var result = await _database.Users.UpdateOneAsync(
            user => user.Id == CurrentUserId,
            Builders<User>.Update.Pull(user => user.FavoriteProductIds, productId));

        return result.MatchedCount == 0 ? Unauthorized() : NoContent();
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}