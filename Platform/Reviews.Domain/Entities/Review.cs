using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;
public sealed class Review : BaseEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxTextLength = 2000;
    public const int MaxPhotos = 5;
    public const int MaxProductNameLength = 200;

    private List<ReviewPhoto> _photos = new();

    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;      
    public Author Author { get; private set; } = null!;                  
    public Guid OrderItemId { get; private set; }                        
    public int Rating { get; private set; }
    public string? Text { get; private set; }
    public ReviewStatus Status { get; private set; }
    public Moderation? Moderation { get; private set; }
    public IReadOnlyList<ReviewPhoto> Photos => _photos.AsReadOnly();
    public Comment? Reply { get; private set; }
    public int ReportCount { get; private set; }
    public Guid CustomerId => Author.Id;

    private Review() { }   

    private Review(Guid productId, string productName, Author customer, Guid orderItemId, int rating, string? text)
        : base($"customer:{customer.Id}")
    {
        ProductId = Guard.NotEmpty(productId, nameof(productId));
        ProductName = Guard.NotBlank(productName, nameof(productName), MaxProductNameLength);
        Author = customer;
        OrderItemId = Guard.NotEmpty(orderItemId, nameof(orderItemId));
        Rating = ValidateRating(rating);
        Text = Guard.OptionalMax(text, nameof(text), MaxTextLength);
        Status = ReviewStatus.Pending;
    }

    public static Review Create(Guid productId, string productName, Author customer,
                                Guid orderItemId, int rating, string? text)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return new Review(productId, productName, customer, orderItemId, rating, text);
    }


    public void Edit(int rating, string? text)
    {
        EnsureNotDeleted();
        if (Status != ReviewStatus.Pending)
            throw new DomainException("Редагувати можна лише відгук, який очікує модерації.");

        Rating = ValidateRating(rating);
        Text = Guard.OptionalMax(text, nameof(text), MaxTextLength);
        Touch($"customer:{CustomerId}");
    }

    public ReviewPhoto AddPhoto(string url, string? caption = null)
    {
        EnsureNotDeleted();
        if (_photos.Count >= MaxPhotos)
            throw new DomainException($"До відгуку можна додати не більше {MaxPhotos} фото.");

        var photo = ReviewPhoto.Create(url, caption);
        _photos.Add(photo);
        Touch($"customer:{CustomerId}");
        return photo;
    }

    public void RemovePhoto(ObjectId photoId)
    {
        EnsureNotDeleted();
        var photo = _photos.FirstOrDefault(p => p.PhotoId == photoId)
            ?? throw new DomainException("Фото не знайдено у цьому відгуку.");
        _photos.Remove(photo);
        Touch($"customer:{CustomerId}");
    }

    
    public void Approve(Guid moderatorId)
    {
        EnsureModeratable();
        Moderation = Moderation.Approve(moderatorId);
        Status = ReviewStatus.Approved;
        Touch($"staff:{moderatorId}");
    }

    public void Reject(Guid moderatorId, string reason)
    {
        EnsureModeratable();
        Moderation = Moderation.Reject(moderatorId, reason);
        Status = ReviewStatus.Rejected;
        Touch($"staff:{moderatorId}");
    }


    public void AddReply(Author author, string text)
    {
        EnsureNotDeleted();
        if (Status != ReviewStatus.Approved)
            throw new DomainException("Відповісти можна лише на схвалений відгук.");
        if (Reply is not null)
            throw new DomainException("На цей відгук уже є відповідь.");

        Reply = Comment.Create(author, text);
        Touch($"staff:{author.Id}");
    }

    public void EditReply(Guid editorId, string text)
    {
        EnsureNotDeleted();
        if (Reply is null)
            throw new DomainException("Відповіді на цей відгук ще немає.");

        Reply.Edit(text, editorId);
        Touch($"staff:{editorId}");
    }

    public void RemoveReply(Guid editorId)
    {
        EnsureNotDeleted();
        if (Reply is null)
            throw new DomainException("Відповіді на цей відгук ще немає.");

        Reply = null;
        Touch($"staff:{editorId}");
    }

    public void RegisterReport()
    {
        EnsureNotDeleted();
        ReportCount++;
    }

    public void RenameProduct(string newName)
    {
        ProductName = Guard.NotBlank(newName, nameof(newName), MaxProductNameLength);
        Touch("system:ProductRenamed");
    }

    public void Delete(string deletedBy)
    {
        if (IsDeleted) throw new DomainException("Відгук уже видалено.");
        MarkDeleted(deletedBy);
    }

    private static int ValidateRating(int rating) =>
        rating is < MinRating or > MaxRating
            ? throw new DomainException($"Оцінка має бути від {MinRating} до {MaxRating}.")
            : rating;

    private void EnsureNotDeleted()
    {
        if (IsDeleted) throw new DomainException("Відгук видалено.");
    }

    private void EnsureModeratable()
    {
        EnsureNotDeleted();
        if (Status != ReviewStatus.Pending)
            throw new DomainException($"Відгук уже оброблено (статус: {Status}).");
    }
}