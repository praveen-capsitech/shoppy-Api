namespace ShoppyApp.DTOs;

public record CreateProductRequest(string Name, string Description, decimal Price, int Stock, string Category, string ImageUrl);
public record UpdateProductRequest(string Name, string Description, decimal Price, int Stock, string Category, string ImageUrl);
public record ProductDetailsResponse(
	string Id,
	string Name,
	string Description,
	decimal Price,
	int Stock,
	string Category,
	string ImageUrl,
	DateTime CreatedAt,
	string OwnerName);
