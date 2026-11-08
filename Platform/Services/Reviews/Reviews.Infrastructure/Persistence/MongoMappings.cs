using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    /// <summary>Єдине місце, де задано правило іменування елементів BSON (camelCase).</summary>
    private static readonly ConventionPack CamelCasePack = new() { new CamelCaseElementNameConvention() };

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            // Guid → binData subtype 4 (UUID), як у схемі (bsonType: "binData").
            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            // Глобальна конвенція: імена елементів BSON у camelCase (CreatedAt → createdAt) для всіх типів цієї збірки.
            // Для типів без явного мапінгу (їх драйвер мапить сам) цього достатньо; явні мапінги отримують те саме правило через MapClass.
            var domainAssembly = typeof(Review).Assembly;
            ConventionRegistry.Register("ReviewsCamelCase", CamelCasePack, type => type.Assembly == domainAssembly);

            MapBaseEntity();
            MapAuthor();
            MapModeration();
            MapReviewPhoto();
            MapComment();
            MapReview();

            _registered = true;
        }
    }

    /// <summary>Реєструє явний мапінг класу й застосовує до його членів той самий пакет camelCase.
    /// Конвенції з реєстру драйвер запускає лише для автомапінгу (<c>AutoMap()</c>), а для явного
    /// <c>RegisterClassMap</c> ні. Автомапінг тут небажаний: домен має приватні сетери та конструктори,
    /// і повний пакет конвенцій за замовчуванням сам домапив би зайве. Тому застосовуємо лише наш пакет
    /// до вже заданих членів: імена задає конвенція, а не ручні <c>SetElementName</c>.</summary>
    private static void MapClass<T>(Action<BsonClassMap<T>> map, Action<BsonClassMap<T>>? afterConventions = null)
    {
        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            map(cm);

            foreach (var memberMap in cm.DeclaredMemberMaps)
                foreach (var convention in CamelCasePack.Conventions.OfType<IMemberMapConvention>())
                    convention.Apply(memberMap);

            // Ідентифікатор документа в MongoDB завжди _id, а не "id".
            cm.IdMemberMap?.SetElementName("_id");

            afterConventions?.Invoke(cm);
        });
    }

    private static void MapBaseEntity() =>
        MapClass<BaseEntity>(cm =>
        {
            cm.MapIdMember(x => x.Id);                                  
            cm.MapProperty(x => x.CreatedAt);
            cm.MapProperty(x => x.UpdatedAt);
            cm.MapProperty(x => x.CreatedBy);
            cm.MapProperty(x => x.UpdatedBy);
            cm.MapProperty(x => x.IsDeleted);
            cm.MapProperty(x => x.Version);    
            cm.SetIgnoreExtraElements(true);                           
        });

    private static void MapAuthor() =>
        MapClass<Author>(cm =>
        {
            cm.MapProperty(x => x.Id);
            cm.MapProperty(x => x.DisplayName);
            cm.MapConstructor(
                typeof(Author).GetConstructor(new[] { typeof(Guid), typeof(string) })!,
                nameof(Author.Id), nameof(Author.DisplayName));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapModeration() =>
        MapClass<Moderation>(cm =>
        {
            cm.MapProperty(x => x.ModeratedAt);
            cm.MapProperty(x => x.ModeratorId);
            cm.MapProperty(x => x.Reason).SetIgnoreIfNull(true);
            cm.MapConstructor(
                typeof(Moderation).GetConstructor(new[] { typeof(DateTime), typeof(Guid), typeof(string) })!,
                nameof(Moderation.ModeratedAt), nameof(Moderation.ModeratorId), nameof(Moderation.Reason));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapReviewPhoto() =>
        MapClass<ReviewPhoto>(cm =>
        {
            cm.MapProperty(x => x.PhotoId);
            cm.MapProperty(x => x.Url);
            cm.MapProperty(x => x.Caption).SetIgnoreIfNull(true);
            cm.MapProperty(x => x.UploadedAt);
            cm.MapConstructor(
                typeof(ReviewPhoto).GetConstructor(new[] { typeof(ObjectId), typeof(string), typeof(string), typeof(DateTime) })!,
                nameof(ReviewPhoto.PhotoId), nameof(ReviewPhoto.Url), nameof(ReviewPhoto.Caption), nameof(ReviewPhoto.UploadedAt));
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapComment() =>
        MapClass<Comment>(cm =>
        {
            cm.MapProperty(x => x.Text);
            cm.MapProperty(x => x.Author);      
            cm.SetIgnoreExtraElements(true);
        });

    private static void MapReview() =>
        MapClass<Review>(cm =>
        {
            cm.MapProperty(x => x.ProductId);
            cm.MapProperty(x => x.ProductName);
            cm.MapProperty(x => x.Author);
            cm.MapProperty(x => x.OrderItemId);
            cm.MapProperty(x => x.Rating);
            cm.MapProperty(x => x.Text).SetIgnoreIfNull(true);
            cm.MapProperty(x => x.Status)
              .SetSerializer(new EnumSerializer<ReviewStatus>(BsonType.String));   
            cm.MapProperty(x => x.Moderation).SetIgnoreIfNull(true);
            cm.MapField("_photos");                        
            cm.MapProperty(x => x.Reply).SetIgnoreIfNull(true);
            cm.MapProperty(x => x.ReportCount);
            cm.SetIgnoreExtraElements(true);
        },
        // Приватне поле _photos: camelCase дав би "_photos", а в схемі колекції елемент називається "photos".
        afterConventions: cm => cm.GetMemberMap("_photos").SetElementName("photos"));
}