using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll;

public static class SortParser
{
    public const int MaxTerms = 3;

    public static bool TryParse(string? sort, string? sortBy, string? sortDir, IReadOnlyCollection<string> allowed,
        out IReadOnlyList<SortTerm> terms, out string? error)
    {
        terms = [];
        error = null;

        var hasSort = !string.IsNullOrWhiteSpace(sort);
        var hasSortBy = !string.IsNullOrWhiteSpace(sortBy);
        var hasSortDir = !string.IsNullOrWhiteSpace(sortDir);

        if (hasSort && (hasSortBy || hasSortDir))
        {
            error = "Використовуйте або 'sort', або пару 'sortBy' і 'sortDir', але не обидва записи одразу.";
            return false;
        }

        var result = new List<SortTerm>();

        if (hasSort)
        {
            var parts = sort!.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length > MaxTerms)
            {
                error = $"Сортувати можна не більше ніж за {MaxTerms} полями.";
                return false;
            }

            foreach (var part in parts)
            {
                var descending = part.StartsWith('-');
                var name = part.TrimStart('-', '+').Trim();
                if (!TryAdd(result, name, descending, allowed, out error)) return false;
            }
        }
        else if (hasSortBy)
        {
            var descending = false;
            if (hasSortDir)
            {
                if (sortDir!.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase)) descending = true;
                else if (!sortDir.Trim().Equals("asc", StringComparison.OrdinalIgnoreCase))
                {
                    error = "sortDir: допустимі значення 'asc' і 'desc'.";
                    return false;
                }
            }

            if (!TryAdd(result, sortBy!.Trim(), descending, allowed, out error)) return false;
        }
        else if (hasSortDir)
        {
            error = "sortDir задано без sortBy.";
            return false;
        }

        terms = result;
        return true;
    }
    public static IReadOnlyList<SortTerm> Parse(string? sort, string? sortBy, string? sortDir, IReadOnlyCollection<string> allowed)
    => TryParse(sort, sortBy, sortDir, allowed, out var terms, out var error)
            ? terms
            : throw new InvalidQueryException(error!);

    private static bool TryAdd(List<SortTerm> result, string name, bool descending,
        IReadOnlyCollection<string> allowed, out string? error)
    {
        error = null;
        var canonical = allowed.FirstOrDefault(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
        {
            error = $"Сортування за полем '{name}' не підтримується. Допустимі поля: {string.Join(", ", allowed.Order())}.";
            return false;
        }

        if (result.Any(t => t.Field == canonical))
        {
            error = $"Поле '{canonical}' указано в сортуванні кілька разів.";
            return false;
        }

        result.Add(new SortTerm(canonical, descending));
        return true;
    }
}
