using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Orders.Domain.Specification;

public sealed class PromoCodeListSpecification : Specification<PromoCode>
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<PromoCode, object>>> SortMap =
        new Dictionary<string, Expression<Func<PromoCode, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = p => p.Code,
            ["discountValue"] = p => p.DiscountValue,
            ["validFrom"] = p => p.ValidFrom,
            ["validTo"] = p => p.ValidTo!,
            ["usedCount"] = p => p.Usage.UsedCount,
            ["createdAt"] = p => p.CreatedAt
        };

    public static IReadOnlyCollection<string> SortableFields => SortMap.Keys.ToArray();

    public static readonly IReadOnlyList<SortTerm> DefaultSort = [new("createdAt", true)];
    public PromoCodeListSpecification(string? search, bool? isActive, DiscountType? discountType, DateTime? validOn,
        IReadOnlyList<SortTerm> sort, int page, int pageSize)
    {
        Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToUpperInvariant();  
            Where(p => p.Code.Contains(text));
        }

        if (isActive is { } active)
            Where(p => p.IsActive == active);

        if (discountType is { } type)
            Where(p => p.DiscountType == type);

        if (validOn is { } moment)
            Where(p => p.ValidFrom <= moment && (p.ValidTo == null || p.ValidTo >= moment));

        Include(nameof(PromoCode.Usage));
        UseNoTracking();

        ApplySort(sort, SortMap, DefaultSort);
        OrderBy(p => p.Id);   

        Paginate(page, pageSize);
    }
}
