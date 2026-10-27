using Orders.Domain;

namespace Orders.Domain.Promotions;

public sealed class PromoCodeUsage
{
    internal PromoCodeUsage() { UpdatedAt = DateTime.UtcNow; }

    public Guid PromoCodeId { get; private set; }
    public int UsedCount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    internal void Increment(int? maxUses)
    {
        if (maxUses is not null && UsedCount >= maxUses)
            throw new DomainException("Ліміт використань промокоду вичерпано.");
        UsedCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
