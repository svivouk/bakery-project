using MongoDB.Bson;
using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;
public sealed class ReviewPhoto : ValueObject
{
    public const int MaxCaptionLength = 250;
    public const int MaxUrlLength = 2048;

    public ObjectId PhotoId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public DateTime UploadedAt { get; private set; }

    public ReviewPhoto(ObjectId photoId, string url, string? caption, DateTime uploadedAt)
    {
        if (photoId == ObjectId.Empty)
            throw new Exceptions.DomainException("photoId не може бути порожнім.");
        PhotoId = photoId;
        Url = Guard.NotBlank(url, nameof(url), MaxUrlLength);
        Caption = Guard.OptionalMax(caption, nameof(caption), MaxCaptionLength);
        UploadedAt = Guard.UtcMillis(uploadedAt, nameof(uploadedAt));
    }
    public static ReviewPhoto Create(string url, string? caption = null) =>
        new(ObjectId.GenerateNewId(), url, caption, DateTime.UtcNow);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PhotoId;
        yield return Url;
        yield return Caption;
        yield return UploadedAt;
    }
}