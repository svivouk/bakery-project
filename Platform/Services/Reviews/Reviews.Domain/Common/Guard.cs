using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Common;
internal static class Guard
{
    public static string NotBlank(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} не може бути порожнім.");
        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{name} не може перевищувати {maxLength} символів.");
        return value;
    }

    public static string? OptionalMax(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{name} не може перевищувати {maxLength} символів.");
        return value;
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} не може бути порожнім ідентифікатором.");
        return value;
    }
    public static DateTime UtcMillis(DateTime value, string name)
    {
        if (value == default)
            throw new DomainException($"{name} має бути заданою датою.");
        var utc = value.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}