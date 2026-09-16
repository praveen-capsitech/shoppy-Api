using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShoppyApp.Data;
using ShoppyApp.DTOs;
using ShoppyApp.Models;
using ShoppyApp.Services;

namespace ShoppyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _database;
    private readonly TokenService _tokenService;

    public AuthController(MongoDbContext database, TokenService tokenService)
    {
        _database = database;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!HasValidRegistrationDetails(request))
            return BadRequest(new { message = "Name, email and a password of at least 6 characters are required." });

        var role = GetRequestedRole(request);
        if (role is null)
            return BadRequest(new { message = "A new account can only be registered as a customer or manager." });

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _database.Users.Find(user => user.Email == email).AnyAsync();
        if (exists) return Conflict(new { message = "An account with this email already exists." });

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = role != UserRoles.Manager
        };

        try
        {
            await _database.Users.InsertOneAsync(user);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        if (role == UserRoles.Manager)
            return Accepted(new { message = "Your manager account was created and is awaiting admin approval." });

        return Ok(ToResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!HasValidLoginDetails(request))
            return BadRequest(new { message = "Email and password are required." });

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _database.Users.Find(item => item.Email == email).FirstOrDefaultAsync();
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
        {
            var message = user.Role == UserRoles.Manager
                ? "Your manager account is awaiting admin approval."
                : "Your account is disabled.";

            return Unauthorized(new { message });
        }

        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (id is null) return Unauthorized();

        var user = await _database.Users.Find(item => item.Id == id).FirstOrDefaultAsync();
        if (user is null || !user.IsActive) return Unauthorized();
        return Ok(ToResponse(user));
    }

    private static bool HasValidRegistrationDetails(RegisterRequest request)
    {
        return request is not null &&
               !string.IsNullOrWhiteSpace(request.Name) &&
               !string.IsNullOrWhiteSpace(request.Email) &&
               !string.IsNullOrWhiteSpace(request.Password) &&
               request.Password.Length >= 6;
    }

    private static string? GetRequestedRole(RegisterRequest request)
    {
        var role = string.IsNullOrWhiteSpace(request.Role)
            ? UserRoles.Customer
            : request.Role.Trim();

        return role is UserRoles.Customer or UserRoles.Manager ? role : null;
    }

    private static bool HasValidLoginDetails(LoginRequest request)
    {
        return request is not null &&
               !string.IsNullOrWhiteSpace(request.Email) &&
               !string.IsNullOrWhiteSpace(request.Password);
    }

    private AuthResponse ToResponse(User user)
    {
        var responseUser = new UserResponse(user.Id, user.Name, user.Email, user.Role);
        return new AuthResponse(_tokenService.CreateToken(user), responseUser);
    }
}
