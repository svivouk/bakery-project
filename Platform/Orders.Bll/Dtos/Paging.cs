using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Orders.Bll.Dtos;

public abstract class PagedQuery : IValidatableObject
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 1_000_000;

    [Range(1, MaxPage, ErrorMessage = "page: значення має бути від 1 до 1000000.")]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = "pageSize: значення має бути не менше 1.")]
    public int PageSize { get; set; } = DefaultPageSize;

    public string? Sort { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int GetPageSize() => Math.Min(PageSize, MaxPageSize);
    protected abstract IReadOnlyCollection<string> GetSortableFields();
    public IReadOnlyList<SortTerm> ResolveSort() => SortParser.Parse(Sort, SortBy, SortDir, GetSortableFields());

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SortParser.TryParse(Sort, SortBy, SortDir, GetSortableFields(), out _, out var error))
        {
            var member = string.IsNullOrWhiteSpace(Sort) ? (string.IsNullOrWhiteSpace(SortBy) ? nameof(SortDir) : nameof(SortBy)) : nameof(Sort);
            yield return new ValidationResult(error, [member]);
        }

        foreach (var result in ValidateFilters()) yield return result;
    }
    protected virtual IEnumerable<ValidationResult> ValidateFilters() => [];
}
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = Items.Select(selector).ToList(),
        Page = Page,
        PageSize = PageSize,
        TotalCount = TotalCount
    };
}