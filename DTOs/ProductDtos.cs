namespace ShoppyApp.DTOs;

public record CreateProductRequest(string Name, string Description, decimal Price, int Stock, string Category, string ImageUrl);
public record UpdateProductRequest(string Name, string Description, decimal Price, int Stock, string Category, string ImageUrl);
