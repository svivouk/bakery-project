using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            // Guid → binData subtype 4 (UUID), як у схемі (bsonType: "binData").
            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            MapBaseEntity();
            MapAuthor();
            MapModeration();
            MapReviewPhoto();
            MapComment();
            MapReview();

            _registered = true;
        }
    }

    private static void MapBaseEntity() =>
        BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
        {
            cm.MapIdMember(x => x.Id);                                  
            cm.MapProperty(x => x.CreatedAt).SetElementName("createdAt");
            cm.MapProperty(x => x.UpdatedAt).SetElementName("updatedAt");
            cm.MapProperty(x => x.CreatedBy).SetElementName("createdBy");
            cm.MapProperty(x => x.UpdatedBy).SetElementName("updatedBy");
            cm.MapProperty(x => x.IsDeleted).SetElementName("isDeleted");
            cm.MapProperty(x => x.Version).SetElementName("version");    
            cm.SetIgnoreExtraElements(true);                           
        });

    private static void MapAuthor() =>
        BsonClassMap.RegisterClassMap<Author>(cm =>
        {
            cm.MapProperty(x => x.Id).SetElementName("id");
            cm.MapProperty(x => x.DisplayName).SetElementName("displayName");
            cm.MapConstructor(
                typeof(Author).GetConstructor(new[] { typeof(Guid), typeof(string) })!,
                nameof(Author.Id), nameof(Author.DisplayName));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapModeration() =>
        BsonClassMap.RegisterClassMap<Moderation>(cm =>
        {
            cm.MapProperty(x => x.ModeratedAt).SetElementName("moderatedAt");
            cm.MapProperty(x => x.ModeratorId).SetElementName("moderatorId");
            cm.MapProperty(x => x.Reason).SetElementName("reason").SetIgnoreIfNull(true);
            cm.MapConstructor(
                typeof(Moderation).GetConstructor(new[] { typeof(DateTime), typeof(Guid), typeof(string) })!,
                nameof(Moderation.ModeratedAt), nameof(Moderation.ModeratorId), nameof(Moderation.Reason));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapReviewPhoto() =>
        BsonClassMap.RegisterClassMap<ReviewPhoto>(cm =>
        {
            cm.MapProperty(x => x.PhotoId).SetElementName("photoId");
            cm.MapProperty(x => x.Url).SetElementName("url");
            cm.MapProperty(x => x.Caption).SetElementName("caption").SetIgnoreIfNull(true);
            cm.MapProperty(x => x.UploadedAt).SetElementName("uploadedAt");
            cm.MapConstructor(
                typeof(ReviewPhoto).GetConstructor(new[] { typeof(ObjectId), typeof(string), typeof(string), typeof(DateTime) })!,
                nameof(ReviewPhoto.PhotoId), nameof(ReviewPhoto.Url), nameof(ReviewPhoto.Caption), nameof(ReviewPhoto.UploadedAt));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapComment() =>
        BsonClassMap.RegisterClassMap<Comment>(cm =>
        {
            cm.MapProperty(x => x.Text).SetElementName("text");
            cm.MapProperty(x => x.Author).SetElementName("author");      
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapReview() =>
        BsonClassMap.RegisterClassMap<Review>(cm =>
        {
            cm.MapProperty(x => x.ProductId).SetElementName("productId");
            cm.MapProperty(x => x.ProductName).SetElementName("productName");
            cm.MapProperty(x => x.Author).SetElementName("author");
            cm.MapProperty(x => x.OrderItemId).SetElementName("orderItemId");
            cm.MapProperty(x => x.Rating).SetElementName("rating");
            cm.MapProperty(x => x.Text).SetElementName("text").SetIgnoreIfNull(true);
            cm.MapProperty(x => x.Status).SetElementName("status")
              .SetSerializer(new EnumSerializer<ReviewStatus>(BsonType.String));   
            cm.MapProperty(x => x.Moderation).SetElementName("moderation").SetIgnoreIfNull(true);
            cm.MapField("_photos").SetElementName("photos");                        
            cm.MapProperty(x => x.Reply).SetElementName("reply").SetIgnoreIfNull(true);
            cm.MapProperty(x => x.ReportCount).SetElementName("reportCount");
            cm.SetIgnoreExtraElements(true);
        });
}