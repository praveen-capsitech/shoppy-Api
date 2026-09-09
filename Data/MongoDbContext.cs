using MongoDB.Driver;
using ShoppyApp.Models;
using ShoppyApp.Settings;

namespace ShoppyApp.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
        Users.Indexes.CreateOne(new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }));
    }
}
