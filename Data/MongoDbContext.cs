using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ShoppyApp.Models;
using ShoppyApp.Settings;

namespace ShoppyApp.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");

    public MongoDbContext(IOptions<MongoDbSettings> options)
    {
        // var mongoUrl = builder.Configuration["ConnectionStrings:MongoDb"];
        // var client = new MongoClient(mongoUrl);

        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        Users.Indexes.CreateOne(new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }));
    }

    // public MongoDbContext(IConfiguration configuration)
    // {
    //     var connectionString =
    //         configuration["MongoDbSettings:ConnectionString"];

    //     var databaseName =
    //         configuration["MongoDbSettings:DatabaseName"];

    //     var collectionName =
    //         configuration["MongoDbSettings:CollectionName"];

    //     var client = new MongoClient(connectionString);
    //     var database = client.GetDatabase(databaseName);
    // }
}
