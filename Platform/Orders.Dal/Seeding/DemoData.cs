using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Seeding;

public static class DemoData
{
    public static readonly Guid CustomerId = new("c1000000-0000-4000-8000-000000000001");
    public static readonly Guid CartId = new("b1000000-0000-4000-8000-000000000001");

    public const string CustomerName = "Ірина Демо";
    public const string CustomerPhone = "380501234567";
    public static readonly Guid CartProductId = ReferenceData.Products[0].Id;
    public static readonly Guid CartVariantId = ReferenceData.Variants[0].Id;
    public const int CartQuantity = 2;

    public sealed record PromoSeed(string Code, DiscountType Type, decimal Value, decimal? MinOrderAmount, int? MaxUses);

    public static readonly IReadOnlyList<PromoSeed> PromoCodes =
    [
        new("WELCOME10", DiscountType.Percentage, 10m, null, null),
        new("MINUS50", DiscountType.FixedAmount, 50m, 300m, 100)
    ];
}

