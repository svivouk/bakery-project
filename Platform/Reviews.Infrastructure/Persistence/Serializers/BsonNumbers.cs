using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Reviews.Infrastructure.Persistence.Serializers;

internal static class BsonNumbers
{
    public static long ReadInt64(IBsonReader reader) => reader.CurrentBsonType switch
    {
        BsonType.Int32 => reader.ReadInt32(),
        BsonType.Int64 => reader.ReadInt64(),
        BsonType.Double => (long)reader.ReadDouble(),
        var other => throw new FormatException($"Очікувалось число, отримано {other}.")
    };
}