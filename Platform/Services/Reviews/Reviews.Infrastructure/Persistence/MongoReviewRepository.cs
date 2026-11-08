using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.Models;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Репозиторій відгуків (запис і читання агрегата).
/// <para><b>Оптимістична конкурентність.</b> <see cref="UpdateAsync"/> замінює документ лише якщо в базі ще лежить
/// та версія, яку прочитала сутність: <c>ReplaceOne({ _id, version = ExpectedVersion }, entity)</c>. Сутність під час зміни
/// збільшує <c>Version</c>, тож успішна заміна рухає версію вперед. Якщо <c>MatchedCount == 0</c>, документ уже змінив
/// хтось інший (або його видалено): кидається <see cref="ConcurrencyException"/> (409) чи <see cref="NotFoundException"/> (404).
/// Перевірка й запис відбуваються одним атомарним оператором на сервері, тож між ними не може вклинитись чужа зміна.</para>
/// <para>Усередині <see cref="IUnitOfWork"/> усі виклики йдуть у сесії (транзакції) контексту, поза ним без сесії.</para></summary>
public sealed class MongoReviewRepository(MongoDbContext db) : IReviewRepository
{
    private static readonly FilterDefinitionBuilder<Review> F = Builders<Review>.Filter;
    private static readonly UpdateDefinitionBuilder<Review> U = Builders<Review>.Update;

    private IMongoCollection<Review> Primary => db.Reviews;

    /// <summary>Eventual-читання йдуть на secondary, але лише поза транзакцією: у транзакції дозволено тільки primary.</summary>
    private IMongoCollection<Review> ForRead(ReadConsistency consistency) =>
        consistency == ReadConsistency.Eventual && db.Session is null
            ? Primary.WithReadPreference(ReadPreference.SecondaryPreferred)
            : Primary;

    // ---------------------------------------------------------------- читання

    public async Task<Review?> GetByIdAsync(ObjectId id, ReadConsistency consistency = ReadConsistency.Strong,
        CancellationToken ct = default)
    {
        var filter = F.And(F.Eq(x => x.Id, id), F.Eq(x => x.IsDeleted, false));
        return await FindFirstAsync(ForRead(consistency), filter, ct);
    }

    public async Task<Review?> GetByOrderItemIdAsync(Guid orderItemId, ReadConsistency consistency = ReadConsistency.Strong,
        CancellationToken ct = default)
    {
        var filter = F.And(F.Eq(x => x.OrderItemId, orderItemId), F.Eq(x => x.IsDeleted, false));
        return await FindFirstAsync(ForRead(consistency), filter, ct);
    }

    /// <summary>Без фільтра isDeleted: унікальний індекс orderItemId охоплює й видалені відгуки.</summary>
    public async Task<bool> ExistsByOrderItemIdAsync(Guid orderItemId, CancellationToken ct = default)
        => await CountAsync(Primary, F.Eq(x => x.OrderItemId, orderItemId), ct) > 0;

    public async Task<long> CountAsync(ReviewCriteria criteria, CancellationToken ct = default)
        => await CountAsync(ForRead(ReadConsistency.Eventual), ReviewFilters.Build(criteria), ct, limit: null);

    /// <summary>Сторінка за курсором (keyset): наступна сторінка починається після останнього елемента попередньої,
    /// без Skip, тож вартість не росте з номером сторінки й записи не губляться при вставках.</summary>
    public async Task<Page<Review>> FindAsync(ReviewCriteria criteria, PageRequest page,
        ReadConsistency consistency = ReadConsistency.Eventual, CancellationToken ct = default)
    {
        var filter = ReviewFilters.Build(criteria);
        if (page.Cursor is not null)
            filter = F.And(filter, CursorFilter(criteria.Sort, page.Cursor));

        var collection = ForRead(consistency);
        var find = db.Session is { } s ? collection.Find(s, filter) : collection.Find(filter);

        // limit + 1: зайвий запис лише показує, що є наступна сторінка
        var items = await find.Sort(ReviewFilters.Sort(criteria.Sort)).Limit(page.Limit + 1).ToListAsync(ct);

        var hasNext = items.Count > page.Limit;
        if (hasNext) items.RemoveAt(items.Count - 1);

        return new Page<Review>(items, hasNext ? EncodeCursor(items[^1]) : null);
    }

    /// <summary>Повнотекстовий пошук ($text; потрібен текстовий індекс, див. p3/collections.js), найрелевантніші першими.</summary>
    public async Task<IReadOnlyList<Review>> SearchTextAsync(string query, ReviewCriteria? filter, int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new DomainException("Порожній пошуковий запит.");
        if (limit is < 1 or > PageRequest.MaxLimit)
            throw new DomainException($"Ліміт має бути від 1 до {PageRequest.MaxLimit}.");

        var combined = F.And(F.Text(query), ReviewFilters.Build(filter ?? new ReviewCriteria()));
        var collection = ForRead(ReadConsistency.Eventual);
        var find = db.Session is { } s ? collection.Find(s, combined) : collection.Find(combined);

        return await find
            .Sort(Builders<Review>.Sort.MetaTextScore("score"))
            .Project<Review>(Builders<Review>.Projection.MetaTextScore("score"))
            .Limit(limit)
            .ToListAsync(ct);
    }

    /// <summary>Перерахунок статистики з нуля (довідково/для відновлення зведення): лише схвалені, невидалені відгуки.</summary>
    public async Task<RatingStats> AggregateRatingStatsAsync(Guid productId, CancellationToken ct = default)
    {
        var match = F.And(
            F.Eq(x => x.ProductId, productId),
            F.Eq(x => x.IsDeleted, false),
            F.Eq(x => x.Status, ReviewStatus.Approved));

        var aggregate = db.Session is { } s ? ForRead(ReadConsistency.Strong).Aggregate(s) : ForRead(ReadConsistency.Eventual).Aggregate();
        var groups = await aggregate
            .Match(match)
            .Group(x => x.Rating, g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var distribution = Enumerable.Range(Review.MinRating, Review.MaxRating).ToDictionary(r => r, _ => 0);
        foreach (var g in groups) distribution[g.Rating] = g.Count;

        return new RatingStats(distribution.Values.Sum(), distribution.Sum(p => p.Key * p.Value), distribution);
    }

    // ----------------------------------------------------------------- запис

    public async Task AddAsync(Review review, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        try
        {
            if (db.Session is { } s) await Primary.InsertOneAsync(s, review, cancellationToken: ct);
            else await Primary.InsertOneAsync(review, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey
                                             && ex.WriteError.Message.Contains("orderItemId", StringComparison.Ordinal))
        {
            // Унікальний індекс orderItemId: інший запит устиг раніше, ніж спрацювала перевірка в валідаторі.
            throw new DuplicateReviewException(review.OrderItemId);
        }

        review.MarkPersisted();   // у базі тепер Version сутності, наступна зміна порівнюватиме саме з нею
    }

    /// <summary>Заміна документа з перевіркою версії. <c>MatchedCount == 0</c> означає, що за фільтром
    /// (<c>_id</c> + очікувана <c>version</c>) нічого не знайдено.</summary>
    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        var expectedVersion = review.ExpectedVersion;   // версія, яку сутність прочитала з бази
        var filter = F.And(F.Eq(x => x.Id, review.Id), F.Eq(x => x.Version, expectedVersion));

        var result = db.Session is { } s
            ? await Primary.ReplaceOneAsync(s, filter, review, new ReplaceOptions(), ct)
            : await Primary.ReplaceOneAsync(filter, review, new ReplaceOptions(), ct);

        if (result.MatchedCount == 0)
        {
            // Розрізняємо «документа немає» і «документ є, але з іншою версією».
            var exists = await CountAsync(Primary, F.Eq(x => x.Id, review.Id), ct) > 0;
            throw exists
                ? new ConcurrencyException(nameof(Review), review.Id, expectedVersion)
                : new NotFoundException(nameof(Review), review.Id);
        }

        review.MarkPersisted();
    }

    /// <summary>Лічильник скарг: атомарний <c>$inc</c> без перевірки версії. Це монотонний лічильник, а не редагування
    /// тексту, тому паралельні скарги не мають конфліктувати між собою й скасовувати правки автора.</summary>
    public async Task IncrementReportCountAsync(ObjectId reviewId, CancellationToken ct = default)
    {
        var filter = F.And(F.Eq(x => x.Id, reviewId), F.Eq(x => x.IsDeleted, false));
        var update = U.Inc(x => x.ReportCount, 1);

        var result = db.Session is { } s
            ? await Primary.UpdateOneAsync(s, filter, update, cancellationToken: ct)
            : await Primary.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (result.MatchedCount == 0) throw new NotFoundException(nameof(Review), reviewId);
    }

    /// <summary>Масове оновлення копії назви товару (подія ProductRenamed). Версія збільшується, тож автор, що редагував
    /// відгук паралельно, отримає конфлікт і перечитає документ, а не затре нову назву старою.</summary>
    public async Task<long> RenameProductAsync(Guid productId, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Length > Review.MaxProductNameLength)
            throw new DomainException($"Назва товару має бути непорожньою й не довшою за {Review.MaxProductNameLength} символів.");

        var filter = F.And(F.Eq(x => x.ProductId, productId), F.Ne(x => x.ProductName, newName));   // повтор події нічого не змінює
        var update = U.Combine(
            U.Set(x => x.ProductName, newName),
            U.Set(x => x.UpdatedAt, DateTime.UtcNow),
            U.Set(x => x.UpdatedBy, "system:ProductRenamed"),
            U.Inc(x => x.Version, 1));

        var result = db.Session is { } s
            ? await Primary.UpdateManyAsync(s, filter, update, cancellationToken: ct)
            : await Primary.UpdateManyAsync(filter, update, cancellationToken: ct);

        return result.ModifiedCount;
    }

    // ------------------------------------------------------------ допоміжне

    private async Task<Review?> FindFirstAsync(IMongoCollection<Review> collection, FilterDefinition<Review> filter, CancellationToken ct)
    {
        var find = db.Session is { } s ? collection.Find(s, filter) : collection.Find(filter);
        return await find.FirstOrDefaultAsync(ct);
    }

    private async Task<long> CountAsync(IMongoCollection<Review> collection, FilterDefinition<Review> filter,
        CancellationToken ct, int? limit = 1)
    {
        var options = new CountOptions { Limit = limit };
        return db.Session is { } s
            ? await collection.CountDocumentsAsync(s, filter, options, ct)
            : await collection.CountDocumentsAsync(filter, options, ct);
    }

    // --- курсор: "рейтинг|createdAt(ticks)|id" у base64url ---

    private static string EncodeCursor(Review r)
    {
        var raw = $"{r.Rating}|{r.CreatedAt.ToUniversalTime().Ticks}|{r.Id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static FilterDefinition<Review> CursorFilter(ReviewSort sort, string cursor)
    {
        int rating;
        DateTime createdAt;
        ObjectId id;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split('|');
            rating = int.Parse(parts[0]);
            createdAt = new DateTime(long.Parse(parts[1]), DateTimeKind.Utc);
            id = ObjectId.Parse(parts[2]);
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException or ArgumentException)
        {
            throw new DomainException("Некоректний курсор сторінки.");
        }

        // Ключі сортування в тому самому порядку, що й у ReviewFilters.Sort: (поле, спадання, значення курсора)
        (string Field, bool Desc, BsonValue Value)[] keys = sort switch
        {
            ReviewSort.Oldest => [("createdAt", false, new BsonDateTime(createdAt)), ("_id", false, id)],
            ReviewSort.RatingHigh => [("rating", true, rating), ("createdAt", true, new BsonDateTime(createdAt)), ("_id", true, id)],
            ReviewSort.RatingLow => [("rating", false, rating), ("createdAt", true, new BsonDateTime(createdAt)), ("_id", true, id)],
            _ => [("createdAt", true, new BsonDateTime(createdAt)), ("_id", true, id)]
        };

        // (k1 після c1) АБО (k1 = c1 І k2 після c2) АБО ...
        var branches = new BsonArray();
        for (var i = 0; i < keys.Length; i++)
        {
            var branch = new BsonDocument();
            for (var j = 0; j < i; j++) branch.Add(keys[j].Field, keys[j].Value);
            branch.Add(keys[i].Field, new BsonDocument(keys[i].Desc ? "$lt" : "$gt", keys[i].Value));
            branches.Add(branch);
        }

        return new BsonDocument("$or", branches);
    }
}
