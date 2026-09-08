using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ShoppyApp.Models;

namespace ShoppyApp.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAdminAsync(MongoDbContext db, IConfiguration config)
    {
        var section = config.GetSection("SeedAdmin");
       
        if (!section.GetValue<bool>("Enabled")) return;
       
        var email = section["Email"]?.Trim().ToLowerInvariant();
       
        var password = section["Password"];
        
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        
        var existing = await db.Users.Find(x => x.Email == email).FirstOrDefaultAsync();
        
        if (existing is not null) return;
        
        await db.Users.InsertOneAsync(new User
        {
            Name = section["Name"] ?? "System Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRoles.Admin,
            IsActive = true
        });
    }
}
