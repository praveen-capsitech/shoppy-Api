using MongoDB.Driver;

public class AuthService
{
    private readonly IMongoCollection<ShoppyItem> _shoppy;

    public AuthService(IConfiguration configuration)
    {
        var connectionString =
            configuration["MongoDbSettings:ConnectionString"];

        var databaseName =
            configuration["MongoDbSettings:DatabaseName"];

        var collectionName =
            configuration["MongoDbSettings:CollectionName"];

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);

        _shoppy = database.GetCollection<ShoppyItem>(collectionName);
    }

}