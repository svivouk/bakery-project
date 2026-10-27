using System.Text.RegularExpressions;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;
internal static partial class Check
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex SlugRegex();

    public static string Text(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"Поле '{field}' обов'язкове.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException($"Поле '{field}' не може бути довшим за {maxLength} символів.");
        return trimmed;
    }

    public static string? OptionalText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException($"Поле '{field}' не може бути довшим за {maxLength} символів.");
        return trimmed;
    }

    ///  Slug: лише a-z, 0-9 і дефіс (CK_*_Slug). 
    public static string Slug(string? value, string field, int maxLength)
    {
        var slug = Text(value, field, maxLength);
        if (!SlugRegex().IsMatch(slug))
            throw new ValidationException($"Поле '{field}' може містити лише малі латинські літери, цифри та дефіс.");
        return slug;
    }

    public static decimal Price(decimal value, string field)
    {
        if (value < 0 || value > 99_999_999.99m)
            throw new ValidationException($"Поле '{field}' має бути в діапазоні від 0 до 99999999.99.");
        return decimal.Round(value, 2);
    }

    public static int? PositiveOrNull(int? value, string field)
    {
        if (value is <= 0)
            throw new ValidationException($"Поле '{field}' має бути більшим за нуль.");
        return value;
    }

    public static int NonNegative(int value, string field)
    {
        if (value < 0)
            throw new ValidationException($"Поле '{field}' не може бути від'ємним.");
        return value;
    }

    public static byte[] RowVersion(byte[]? value)
    {
        if (value is null || value.Length != 8)
            throw new ValidationException("Поле 'rowVersion' обов'язкове (8 байтів, base64) — токен конкурентності.");
        return value;
    }
}
