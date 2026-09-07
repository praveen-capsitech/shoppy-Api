using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class ShoppyItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Title")]
    public string Title { get; set; } = null!;

    public bool IsCompleted { get; set; }
}