using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShoppyApp.Data;
using ShoppyApp.DTOs;
using ShoppyApp.Models;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly MongoDbContext _db;
    public ProductsController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll() => Ok(await _db.Products.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> Get(string id)
    {
        var product = await _db.Products.Find(x => x.Id == id).FirstOrDefaultAsync();
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductRequest request)
    {
        if (!IsValid(request.Name, request.Description, request.Category, request.ImageUrl, request.Price, request.Stock))
            return BadRequest(new { message = "Product fields are invalid." });

        var product = new Product { Name = request.Name, Description = request.Description, Price = request.Price, Stock = request.Stock, Category = request.Category, ImageUrl = request.ImageUrl };
        await _db.Products.InsertOneAsync(product);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> Update(string id, UpdateProductRequest request)
    {
        if (!IsValid(request.Name, request.Description, request.Category, request.ImageUrl, request.Price, request.Stock))
            return BadRequest(new { message = "Product fields are invalid." });

        var update = Builders<Product>.Update
            .Set(x => x.Name, request.Name).Set(x => x.Description, request.Description).Set(x => x.Price, request.Price)
            .Set(x => x.Stock, request.Stock).Set(x => x.Category, request.Category).Set(x => x.ImageUrl, request.ImageUrl);
        var product = await _db.Products.FindOneAndUpdateAsync(x => x.Id == id, update, new FindOneAndUpdateOptions<Product> { ReturnDocument = ReturnDocument.After });
        return product is null ? NotFound() : Ok(product);
    }

    private static bool IsValid(string name, string description, string category, string imageUrl, decimal price, int stock) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 200 &&
        !string.IsNullOrWhiteSpace(description) && description.Length <= 2000 &&
        !string.IsNullOrWhiteSpace(category) && category.Length <= 100 &&
        !string.IsNullOrWhiteSpace(imageUrl) && imageUrl.Length <= 2000 &&
        Uri.TryCreate(imageUrl, UriKind.Absolute, out _) &&
        price >= 0 && stock >= 0;

    [Authorize(Roles = "Manager,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _db.Products.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }
}
