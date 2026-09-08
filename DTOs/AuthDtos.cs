namespace ShoppyApp.DTOs;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record UserResponse(string Id, string Name, string Email, string Role);
public record AuthResponse(string Token, UserResponse User);
