using System.Text.RegularExpressions;

namespace Orders.Domain;

public static partial class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v))
            throw new DomainException($"{field}: значення обов'язкове.");
        if (v.Length > maxLength)
            throw new DomainException($"{field}: максимум {maxLength} символів.");
        return v;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLength)
            throw new DomainException($"{field}: максимум {maxLength} символів.");
        return v;
    }

    public static Guid NotEmpty(Guid value, string field)
        => value == Guid.Empty ? throw new DomainException($"{field}: ідентифікатор не задано.") : value;

    public static int Positive(int value, string field)
        => value > 0 ? value : throw new DomainException($"{field}: має бути більше нуля.");

    public static decimal Money(decimal value, string field, int precision = 10)
    {
        if (value < 0)
            throw new DomainException($"{field}: не може бути від'ємною.");
        if (decimal.Round(value, 2) != value)
            throw new DomainException($"{field}: допускається щонайбільше 2 знаки після коми.");
        var limit = 1m;
        for (var i = 0; i < precision - 2; i++) limit *= 10;
        if (value >= limit)
            throw new DomainException($"{field}: перевищує допустиме значення.");
        return value;
    }
    public static string Phone(string? value, string field)
    {
        var v = Required(value, field, 20);
        if (!PhonePattern().IsMatch(v) || v.Length is < 10 or > 15)
            throw new DomainException($"{field}: очікується 10–15 символів, лише цифри та '+'.");
        return v;
    }

    [GeneratedRegex("^[0-9+]+$")]
    private static partial Regex PhonePattern();
}
