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
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || request.Password.Length < 6)
            return BadRequest(new { message = "Name, email and a password of at least 6 characters are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.Find(x => x.Email == email).AnyAsync();
        if (exists) return Conflict(new { message = "An account with this email already exists." });

        var user = new User { Name = request.Name.Trim(), Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Role = UserRoles.Customer };
        await _db.Users.InsertOneAsync(user);
        return Ok(ToResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Find(x => x.Email == email).FirstOrDefaultAsync();
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });
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
