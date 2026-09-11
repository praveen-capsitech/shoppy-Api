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
    private readonly MongoDbContext _db;
    private readonly TokenService _tokens;
    public AuthController(MongoDbContext db, TokenService tokens) { _db = db; _tokens = tokens; }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 6)
            return BadRequest(new { message = "Name, email and a password of at least 6 characters are required." });

        var role = string.IsNullOrWhiteSpace(request.Role) ? UserRoles.Customer : request.Role.Trim();
        if (role is not UserRoles.Customer and not UserRoles.Manager)
            return BadRequest(new { message = "A new account can only be registered as a customer or manager." });

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.Find(x => x.Email == email).AnyAsync();
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
            await _db.Users.InsertOneAsync(user);
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
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Find(x => x.Email == email).FirstOrDefaultAsync();
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });
        if (!user.IsActive)
            return Unauthorized(new { message = user.Role == UserRoles.Manager
                ? "Your manager account is awaiting admin approval."
                : "Your account is disabled." });
        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (id is null) return Unauthorized();
        var user = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (user is null || !user.IsActive) return Unauthorized();
        return Ok(ToResponse(user));
    }

    private AuthResponse ToResponse(User user) => new(_tokens.CreateToken(user), new UserResponse(user.Id, user.Name, user.Email, user.Role));
}
