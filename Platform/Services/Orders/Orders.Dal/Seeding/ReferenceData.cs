using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Seeding;


public static class ReferenceData
{
    public static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public sealed record Product(Guid Id, string Name);

    public sealed record Variant(Guid Id, Guid ProductId, string Name, decimal Price);

    private static Guid P(int n) => new($"a0000000-0000-4000-8000-{n:D12}");
    private static Guid V(int n) => new($"d0000000-0000-4000-8000-{n:D12}");

    public static readonly IReadOnlyList<Product> Products =
    [
        new(P(1), "Медовик"),
        new(P(2), "Наполеон"),
        new(P(3), "Круасан класичний"),
        new(P(4), "Хліб на заквасці"),
        new(P(5), "Брауні безглютенове")
    ];

    public static readonly IReadOnlyList<Variant> Variants =
    [
        new(V(1), P(1), "1 кг", 650.00m),
        new(V(2), P(1), "1,5 кг", 950.00m),
        new(V(3), P(2), "1 кг", 600.00m),
        new(V(4), P(3), "1 шт", 55.00m),
        new(V(5), P(3), "6 шт", 300.00m),
        new(V(6), P(4), "500 г", 90.00m),
        new(V(7), P(5), "6 порцій", 420.00m)
    ];
}
