using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence.Serializers;
public sealed class OutboxMessageSerializer : SerializerBase<OutboxMessage>, IBsonDocumentSerializer
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, OutboxMessage value)
    {
        var w = context.Writer;
        var payload = value.Payload as BsonDocument ?? value.Payload.ToBsonDocument(value.Payload.GetType());

        w.WriteStartDocument();
        w.WriteBinaryData("eventId", new BsonBinaryData(value.EventId, GuidRepresentation.Standard));
        w.WriteString("eventType", value.EventType);
        w.WriteBinaryData("aggregateId", new BsonBinaryData(value.AggregateId, GuidRepresentation.Standard));
        w.WriteName("payload");
        BsonDocumentSerializer.Instance.Serialize(context, payload);
        w.WriteDateTime("createdAt", BsonUtils.ToMillisecondsSinceEpoch(value.CreatedAt.ToUniversalTime()));
        w.WriteNull("processedAt");
        w.WriteEndDocument();
    }

    public override OutboxMessage Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var r = context.Reader;
        Guid eventId = default, aggregateId = default;
        var eventType = string.Empty;
        object payload = new BsonDocument();
        var createdAt = DateTime.UnixEpoch;

        r.ReadStartDocument();
        while (r.ReadBsonType() != BsonType.EndOfDocument)
        {
            switch (r.ReadName())
            {
                case "eventId": eventId = r.ReadBinaryData().ToGuid(); break;
                case "eventType": eventType = r.ReadString(); break;
                case "aggregateId": aggregateId = r.ReadBinaryData().ToGuid(); break;
                case "payload": payload = BsonDocumentSerializer.Instance.Deserialize(context); break;
                case "createdAt": createdAt = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(r.ReadDateTime()); break;
                default: r.SkipValue(); break;          
            }
        }
        r.ReadEndDocument();

        return new OutboxMessage(eventId, eventType, aggregateId, payload, createdAt);
    }

    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
    {
        var guid = new GuidSerializer(GuidRepresentation.Standard);
        serializationInfo = memberName switch
        {
            nameof(OutboxMessage.EventId) => new BsonSerializationInfo("eventId", guid, typeof(Guid)),
            nameof(OutboxMessage.AggregateId) => new BsonSerializationInfo("aggregateId", guid, typeof(Guid)),
            nameof(OutboxMessage.EventType) => new BsonSerializationInfo("eventType", new StringSerializer(), typeof(string)),
            nameof(OutboxMessage.CreatedAt) => new BsonSerializationInfo("createdAt", new DateTimeSerializer(DateTimeKind.Utc), typeof(DateTime)),
            _ => null!
        };
        return serializationInfo is not null;
    }
}