using Reviews.Domain.Common;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;
public sealed class Comment : BaseEntity
{
    public const int MaxTextLength = 1000;

    public string Text { get; private set; } = string.Empty;
    public Author Author { get; private set; } = null!;

    private Comment() { }  

    private Comment(Author author, string text) : base($"staff:{author.Id}")
    {
        Author = author;
        Text = Guard.NotBlank(text, nameof(text), MaxTextLength);
    }

    public static Comment Create(Author author, string text)
    {
        ArgumentNullException.ThrowIfNull(author);
        return new Comment(author, text);
    }

    public void Edit(string text, Guid editorId)
    {
        Text = Guard.NotBlank(text, nameof(text), MaxTextLength);
        Touch($"staff:{Guard.NotEmpty(editorId, nameof(editorId))}");
    }
}