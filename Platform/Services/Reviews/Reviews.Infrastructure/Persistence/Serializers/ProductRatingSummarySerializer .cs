using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Models;
using System.Globalization;

namespace Reviews.Infrastructure.Persistence.Serializers;
public sealed class ProductRatingSummarySerializer : SerializerBase<ProductRatingSummary>, IBsonDocumentSerializer
{
    private static readonly GuidSerializer GuidSerializer = new(GuidRepresentation.Standard);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ProductRatingSummary value)
    {
        var w = context.Writer;
        var stats = value.Stats;

        w.WriteStartDocument();
        w.WriteBinaryData("_id", new BsonBinaryData(value.ProductId, GuidRepresentation.Standard));
        w.WriteInt32("count", stats.Count);
        w.WriteInt32("sum", stats.Sum);
        w.WriteDouble("average", stats.Average);

        w.WriteName("distribution");
        w.WriteStartDocument();
        for (var rating = 1; rating <= 5; rating++)
            w.WriteInt32(rating.ToString(CultureInfo.InvariantCulture), stats.Distribution.TryGetValue(rating, out var n) ? n : 0);
        w.WriteEndDocument();

        w.WriteInt64("version", value.Version);
        w.WriteDateTime("updatedAt", BsonUtils.ToMillisecondsSinceEpoch(value.UpdatedAt.ToUniversalTime()));
        w.WriteEndDocument();
    }

    public override ProductRatingSummary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var r = context.Reader;
        Guid productId = default;
        int count = 0, sum = 0;
        long version = 0;
        var updatedAt = DateTime.UnixEpoch;
        var distribution = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0 };

        r.ReadStartDocument();
        while (r.ReadBsonType() != BsonType.EndOfDocument)
        {
            switch (r.ReadName())
            {
                case "_id": productId = r.ReadBinaryData().ToGuid(); break;
                case "count": count = (int)BsonNumbers.ReadInt64(r); break;
                case "sum": sum = (int)BsonNumbers.ReadInt64(r); break;
                case "distribution": ReadDistribution(r, distribution); break;
                case "version": version = BsonNumbers.ReadInt64(r); break;
                case "updatedAt": updatedAt = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(r.ReadDateTime()); break;
                default: r.SkipValue(); break;          // average перераховується, невідомі поля ігноруємо
            }
        }
        r.ReadEndDocument();

        return new ProductRatingSummary(productId, new RatingStats(count, sum, distribution), version, updatedAt);
    }

    private static void ReadDistribution(MongoDB.Bson.IO.IBsonReader r, Dictionary<int, int> distribution)
    {
        r.ReadStartDocument();
        while (r.ReadBsonType() != BsonType.EndOfDocument)
        {
            var key = r.ReadName();
            var value = (int)BsonNumbers.ReadInt64(r);
            if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var rating))
                distribution[rating] = value;
        }
        r.ReadEndDocument();
    }
    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
    {
        serializationInfo = memberName switch
        {
            nameof(ProductRatingSummary.ProductId) => new BsonSerializationInfo("_id", GuidSerializer, typeof(Guid)),
            nameof(ProductRatingSummary.Version) => new BsonSerializationInfo("version", new Int64Serializer(), typeof(long)),
            nameof(ProductRatingSummary.UpdatedAt) => new BsonSerializationInfo("updatedAt", new DateTimeSerializer(DateTimeKind.Utc), typeof(DateTime)),
            _ => null!
        };
        return serializationInfo is not null;
    }
}