using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reviews.Infrastructure.Persistence.Initialization;

public interface IMongoIndexCreator
{
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

/// <summary>Створює всі індекси колекцій одним <c>Indexes.CreateManyAsync</c> на колекцію. Це ідемпотентно: індекс із тим самим
/// ім'ям і тими самими параметрами вдруге не створюється. Якщо ж параметри змінили, а ім'я лишили, MongoDB кине
/// <c>IndexOptionsConflict</c>: тоді старий індекс треба видалити вручну, а нову версію назвати інакше.
/// <para>Набір дзеркалить <c>p3/collections.js</c>; виконується на старті, тож унікальні індекси діють ще до першого запиту.</para></summary>
public sealed class MongoIndexCreator(
    IMongoClient client,
    IOptions<MongoDbSettings> options,
    ILogger<MongoIndexCreator> logger) : IMongoIndexCreator
{
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var db = client.GetDatabase(options.Value.DatabaseName);

        // ---- reviews ----
        await CreateAsync(db, MongoCollections.Reviews, ct,
            // один відгук на покупку (унікальний; діє й на видалені документи)
            Index("UX_Reviews_OrderItem", new BsonDocument("orderItemId", 1), unique: true),
            // відгуки товару: рівність, рівність, сортування (ESR); запити містять isDeleted: false
            Index("IX_Reviews_Product_Status_Created",
                new BsonDocument { { "productId", 1 }, { "status", 1 }, { "createdAt", -1 } },
                partial: new BsonDocument("isDeleted", false)),
            // відгуки автора
            Index("IX_Reviews_Author_Created",
                new BsonDocument { { "author.id", 1 }, { "createdAt", -1 } },
                partial: new BsonDocument("isDeleted", false)),
            // черга модерації
            Index("IX_Reviews_Moderation_Queue", new BsonDocument("createdAt", 1),
                partial: new BsonDocument { { "status", "Pending" }, { "isDeleted", false } }),
            // повнотекстовий пошук (IReviewRepository.SearchTextAsync); без мови: українські слова не стемляться англійським аналізатором
            Index("TX_Reviews_Text", new BsonDocument { { "text", "text" }, { "productName", "text" } },
                defaultLanguage: "none"));

        // ---- reviewReports ----
        await CreateAsync(db, MongoCollections.ReviewReports, ct,
            // одна скарга від клієнта на відгук; префікс reviewId обслуговує «скарги на відгук»
            Index("UX_Reports_Review_Reporter", new BsonDocument { { "reviewId", 1 }, { "reporterCustomerId", 1 } }, unique: true),
            Index("IX_Reports_Open_Queue", new BsonDocument("createdAt", 1),
                partial: new BsonDocument("status", "Open")));

        // ---- reviewEligibility ----
        await CreateAsync(db, MongoCollections.ReviewEligibility, ct,
            Index("UX_Eligibility_OrderItem", new BsonDocument("orderItemId", 1), unique: true),
            Index("IX_Eligibility_Customer_Review", new BsonDocument { { "customerId", 1 }, { "reviewId", 1 } }));

        // ---- outbox / inbox ----
        await CreateAsync(db, MongoCollections.Outbox, ct,
            Index("UX_Outbox_Event", new BsonDocument("eventId", 1), unique: true),
            Index("IX_Outbox_Unprocessed", new BsonDocument("createdAt", 1),
                partial: new BsonDocument("processedAt", new BsonDocument("$type", "null"))));

        await CreateAsync(db, MongoCollections.Inbox, ct,
            // TTL: оброблені вхідні події видаляються через 30 днів
            Index("TTL_Inbox_Processed", new BsonDocument("processedAt", 1), ttl: TimeSpan.FromDays(30)));
    }

    private async Task CreateAsync(IMongoDatabase db, string collectionName, CancellationToken ct,
        params CreateIndexModel<BsonDocument>[] models)
    {
        var names = await db.GetCollection<BsonDocument>(collectionName).Indexes.CreateManyAsync(models, ct);
        logger.LogInformation("Індекси колекції {Collection} перевірено/створено: {Indexes}", collectionName, string.Join(", ", names));
    }

    private static CreateIndexModel<BsonDocument> Index(
        string name, BsonDocument keys, bool unique = false, BsonDocument? partial = null,
        TimeSpan? ttl = null, string? defaultLanguage = null) =>
        new(new BsonDocumentIndexKeysDefinition<BsonDocument>(keys), new CreateIndexOptions<BsonDocument>
        {
            Name = name,
            Unique = unique ? true : null,
            PartialFilterExpression = partial is null ? null : new BsonDocumentFilterDefinition<BsonDocument>(partial),
            ExpireAfter = ttl,
            DefaultLanguage = defaultLanguage
        });
}
