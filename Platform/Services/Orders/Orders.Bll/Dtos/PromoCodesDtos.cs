using Orders.Domain.Promotions;
using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Orders.Bll.Dtos;

public sealed class CreatePromoCodeRequest
{
    /// <summary>Унікальний код (регістр не важливий, зберігається у верхньому).</summary>
    [Required, StringLength(PromoCode.CodeMaxLength, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    /// <summary>Percentage — відсоток (0–100), FixedAmount — фіксована сума.</summary>
    [EnumDataType(typeof(DiscountType))]
    public DiscountType DiscountType { get; init; }

    [Range(0.01, 99999999.99)]
    public decimal DiscountValue { get; init; }

    [Range(0, 99999999.99)]
    public decimal? MinOrderAmount { get; init; }

    public DateTime ValidFrom { get; init; }

    public DateTime? ValidTo { get; init; }

    /// <summary>Скільки разів можна використати; не задано — без ліміту.</summary>
    [Range(1, int.MaxValue)]
    public int? MaxUses { get; init; }
}

/// <summary>Запит на зміну знижки (аналог зміни ціни). Потребує RowVersion, отриманий разом із промокодом.</summary>
public sealed class ChangeDiscountRequest
{
    [EnumDataType(typeof(DiscountType))]
    public DiscountType DiscountType { get; init; }

    [Range(0.01, 99999999.99)]
    public decimal DiscountValue { get; init; }

    /// <summary>Токен версії (base64) із останньої відповіді; захищає від перезапису чужих змін.</summary>
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class PromoCodeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DiscountType { get; init; } = string.Empty;
    public decimal DiscountValue { get; init; }
    public decimal? MinOrderAmount { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public int? MaxUses { get; init; }
    public int UsedCount { get; init; }
    public bool IsActive { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
public sealed class PromoCodeListQuery : PagedQuery
{
    [StringLength(PromoCode.CodeMaxLength, ErrorMessage = "search: не більше 50 символів.")]
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public DiscountType? DiscountType { get; set; }
    public DateTime? ValidOn { get; set; }

    protected override IReadOnlyCollection<string> GetSortableFields() => PromoCodeListSpecification.SortableFields;
}
