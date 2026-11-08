using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>Ідемпотентний сідер: колекцію заповнюємо лише якщо в ній немає жодного документа (перевірка <c>CountDocuments</c> з
/// Limit = 1 перед <c>InsertManyAsync</c>). Повторний запуск і паралельні екземпляри нічого не дублюють: відгуки захищені
/// ще й унікальним індексом orderItemId, а гонка закінчується DuplicateKey, який сідер сприймає як «уже додано».
/// <para>Відгуки створюються через доменні фабрики (<see cref="Review.Create"/>, <c>Approve</c>, <c>AddReply</c>), тож документи
/// в базі мають ту саму форму, що й записані застосунком. Порядок: спершу відгуки, потім залежні від них записи.</para></summary>
public sealed class MongoDataSeeder(
    IMongoClient client,
    IOptions<MongoDbSettings> options,
    ILogger<MongoDataSeeder> logger) : IDataSeeder
{
    private IMongoDatabase Database => client.GetDatabase(options.Value.DatabaseName,
        new MongoDatabaseSettings { WriteConcern = WriteConcern.WMajority });

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var reviews = await SeedReviewsAsync(ct);                 // 1. відгуки
        var eligibility = await SeedEligibilityAsync(ct);          // 2. залежні від відгуків
        var reports = await SeedReportsAsync(ct);
        var summaries = await SeedRatingSummariesAsync(ct);

        logger.LogInformation(
            "Сідінг завершено. Додано: відгуків {Reviews}, прав на відгук {Eligibility}, скарг {Reports}, зведень рейтингу {Summaries}. " +
            "Нуль означає, що колекція вже містила дані.", reviews, eligibility, reports, summaries);
    }

    private async Task<int> SeedReviewsAsync(CancellationToken ct)
    {
        var collection = Database.GetCollection<Review>(MongoCollections.Reviews);
        if (await HasDataAsync(collection, ct)) return 0;

        // Відгук 1: схвалений, з фото, відповіддю і скаргою
        var approved = Review.Create(SeedData.MedovykProductId, "Медовик", new Author(SeedData.Customer1, "Олена К."),
            SeedData.OrderItemMedovyk1, 5, "Дуже ніжний, коржі м'які, не надто солодкий. Доставили вчасно.");
        approved.AddPhoto("https://example.com/photos/medovyk-cut.jpg", "Розріз торта");
        approved.AddPhoto("https://example.com/photos/medovyk-box.jpg");
        approved.Approve(SeedData.Moderator);
        approved.AddReply(new Author(SeedData.Moderator, "Пекарня"), "Дякуємо за теплий відгук! Чекаємо на вас знову.");
        approved.RegisterReport();

        // Відгук 2: очікує модерації
        var pending = Review.Create(SeedData.MedovykProductId, "Медовик", new Author(SeedData.Customer2, "Андрій М."),
            SeedData.OrderItemMedovyk2, 2, "Торт приїхав із запізненням на годину.");

        return await InsertManyOnceAsync(collection, [approved, pending], ct);
    }

    private async Task<int> SeedEligibilityAsync(CancellationToken ct)
    {
        var collection = Database.GetCollection<BsonDocument>(MongoCollections.ReviewEligibility);
        if (await HasDataAsync(collection, ct)) return 0;

        // Залежність від відгуків: reviewId беремо з уже збережених (null, якщо відгуку на цю покупку немає)
        var reviewIds = await ReviewIdsByOrderItemAsync(ct);
        BsonValue ReviewId(Guid orderItemId) => reviewIds.TryGetValue(orderItemId, out var id) ? id : BsonNull.Value;

        return await InsertManyOnceAsync(collection,
        [
            Eligibility(SeedData.Customer1, SeedData.MedovykProductId, SeedData.OrderItemMedovyk1, SeedData.Order1,
                new DateTime(2026, 10, 7, 16, 40, 0, DateTimeKind.Utc), ReviewId(SeedData.OrderItemMedovyk1)),     // використано
            Eligibility(SeedData.Customer2, SeedData.MedovykProductId, SeedData.OrderItemMedovyk2, SeedData.Order2,
                new DateTime(2026, 10, 8, 13, 10, 0, DateTimeKind.Utc), ReviewId(SeedData.OrderItemMedovyk2)),     // використано
            Eligibility(SeedData.Customer1, SeedData.CroissantProductId, SeedData.OrderItemCroissant, SeedData.Order1,
                new DateTime(2026, 10, 7, 16, 40, 0, DateTimeKind.Utc), BsonNull.Value)                           // ще можна оцінити
        ], ct);
    }

    private async Task<int> SeedReportsAsync(CancellationToken ct)
    {
        var collection = Database.GetCollection<BsonDocument>(MongoCollections.ReviewReports);
        if (await HasDataAsync(collection, ct)) return 0;

        var reviewIds = await ReviewIdsByOrderItemAsync(ct);
        if (!reviewIds.TryGetValue(SeedData.OrderItemMedovyk1, out var reviewId)) return 0;

        var created = new DateTime(2026, 10, 9, 11, 20, 0, DateTimeKind.Utc);
        var resolved = new DateTime(2026, 10, 9, 12, 5, 0, DateTimeKind.Utc);
        return await InsertManyOnceAsync(collection,
        [
            new BsonDocument
            {
                { "reviewId", reviewId },
                { "reporterCustomerId", Uuid(SeedData.Customer2) },
                { "reason", "FakeReview" },
                { "comment", "Схоже на рекламний відгук" },
                { "status", "Dismissed" },
                { "createdAt", created },
                { "resolvedAt", resolved },
                { "resolvedBy", Uuid(SeedData.Moderator) },
                { "resolution", "Покупку підтверджено (orderItemId), скаргу відхилено." },
                { "createdBy", $"customer:{SeedData.Customer2}" },
                { "updatedAt", resolved },
                { "updatedBy", $"staff:{SeedData.Moderator}" },
                { "version", 2 }
            }
        ], ct);
    }

    /// <summary>Зведення рейтингу рахується з реально збережених схвалених відгуків, тож узгоджене з ними (а не вигадані числа).</summary>
    private async Task<int> SeedRatingSummariesAsync(CancellationToken ct)
    {
        var collection = Database.GetCollection<BsonDocument>(MongoCollections.ProductRatingSummaries);
        if (await HasDataAsync(collection, ct)) return 0;

        var ratings = await Database.GetCollection<Review>(MongoCollections.Reviews)
            .Find(r => r.ProductId == SeedData.MedovykProductId && !r.IsDeleted && r.Status == ReviewStatus.Approved)
            .Project(r => r.Rating)
            .ToListAsync(ct);
        if (ratings.Count == 0) return 0;

        var distribution = new BsonDocument();
        for (var star = Review.MinRating; star <= Review.MaxRating; star++)
            distribution.Add(star.ToString(), ratings.Count(r => r == star));

        return await InsertManyOnceAsync(collection,
        [
            new BsonDocument
            {
                { "_id", Uuid(SeedData.MedovykProductId) },
                { "count", ratings.Count },
                { "sum", ratings.Sum() },
                { "average", Math.Round(ratings.Average(), 2) },
                { "distribution", distribution },
                { "version", (long)ratings.Count },
                { "updatedAt", DateTime.UtcNow }
            }
        ], ct);
    }

    // ------------------------------------------------------------ допоміжне

    private static async Task<bool> HasDataAsync<T>(IMongoCollection<T> collection, CancellationToken ct) =>
        await collection.CountDocumentsAsync(FilterDefinition<T>.Empty, new CountOptions { Limit = 1 }, ct) > 0;

    /// <summary>InsertMany з перевіркою на гонку: якщо паралельний екземпляр устиг раніше, DuplicateKey не помилка, а «уже додано».</summary>
    private async Task<int> InsertManyOnceAsync<T>(IMongoCollection<T> collection, IReadOnlyCollection<T> documents, CancellationToken ct)
    {
        try
        {
            await collection.InsertManyAsync(documents, cancellationToken: ct);
            return documents.Count;
        }
        catch (MongoBulkWriteException<T> ex) when (ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            logger.LogInformation("Колекція {Collection} уже заповнена іншим екземпляром, вставку пропущено.", collection.CollectionNamespace.CollectionName);
            return 0;
        }
    }

    private async Task<Dictionary<Guid, ObjectId>> ReviewIdsByOrderItemAsync(CancellationToken ct)
    {
        var found = await Database.GetCollection<Review>(MongoCollections.Reviews)
            .Find(r => SeedData.ReviewedOrderItems.Contains(r.OrderItemId))
            .Project(r => new { r.OrderItemId, r.Id })
            .ToListAsync(ct);
        return found.ToDictionary(r => r.OrderItemId, r => r.Id);
    }

    private static BsonDocument Eligibility(Guid customerId, Guid productId, Guid orderItemId, Guid orderId, DateTime since, BsonValue reviewId) =>
        new()
        {
            { "customerId", Uuid(customerId) },
            { "productId", Uuid(productId) },
            { "orderItemId", Uuid(orderItemId) },
            { "orderId", Uuid(orderId) },
            { "eligibleSince", since },
            { "reviewId", reviewId }
        };

    /// <summary>Guid → binData підтипу 4 (UUID), як у схемі колекцій (bsonType: "binData").</summary>
    private static BsonBinaryData Uuid(Guid id) => new(id, GuidRepresentation.Standard);
}
