using Orders.Domain;

namespace Orders.Domain.Promotions;

public enum DiscountType : byte
{
    Percentage = 1,
    FixedAmount = 2
}
public sealed class PromoCode : AuditableEntity
{
    public const int CodeMaxLength = 50;

    private PromoCode() { }

    public PromoCode(string code, DiscountType discountType, decimal discountValue,
        DateTime validFrom, DateTime? validTo = null, decimal? minOrderAmount = null, int? maxUses = null)
    {
        Code = NormalizeCode(code);

        if (!Enum.IsDefined(discountType))
            throw new DomainException("Невідомий тип знижки.");
        DiscountType = discountType;
        DiscountValue = Guard.Money(discountValue, "Розмір знижки");
        if (discountType == DiscountType.Percentage && discountValue > 100)
            throw new DomainException("Відсоток знижки не може перевищувати 100.");

        MinOrderAmount = minOrderAmount is null ? null : Guard.Money(minOrderAmount.Value, "Мінімальна сума замовлення");
        ValidateMaxUses(maxUses);
        MaxUses = maxUses;
        SetPeriod(validFrom, validTo);

        IsActive = true;
        Usage = new PromoCodeUsage();
    }
    public static string NormalizeCode(string? code)
        => Guard.Required(code, "Код", CodeMaxLength).ToUpperInvariant();

    public string Code { get; private set; } = null!;
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal? MinOrderAmount { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public int? MaxUses { get; private set; }

    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public PromoCodeUsage Usage { get; private set; } = null!;
    public void ChangeDiscount(decimal discountValue)
    {
        if (IsDeleted) throw new DomainException("Видалений промокод змінювати не можна.");
        Guard.Money(discountValue, "Розмір знижки");
        if (DiscountType == DiscountType.Percentage && discountValue > 100)
            throw new DomainException("Відсоток знижки не може перевищувати 100.");
        DiscountValue = discountValue;
    }
    public void ChangeDiscount(DiscountType discountType, decimal discountValue)
    {
        if (IsDeleted) throw new InvalidStateException("Видалений промокод змінювати не можна.");
        if (!Enum.IsDefined(discountType))
            throw new DomainException("Невідомий тип знижки.");
        var value = Guard.Money(discountValue, "Розмір знижки");
        if (discountType == DiscountType.Percentage && value > 100)
            throw new DomainException("Відсоток знижки не може перевищувати 100.");
        DiscountType = discountType;
        DiscountValue = value;
    }

    public void Reschedule(DateTime validFrom, DateTime? validTo) => SetPeriod(validFrom, validTo);

    public void ChangeMaxUses(int? maxUses)
    {
        ValidateMaxUses(maxUses);
        if (maxUses is not null && maxUses < Usage.UsedCount)
            throw new DomainException("Ліміт не може бути меншим за вже використану кількість.");
        MaxUses = maxUses;
    }

    public void Activate()
    {
        if (IsDeleted) throw new InvalidStateException("Видалений промокод не можна активувати.");
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
    }
    public decimal CalculateDiscount(decimal subtotal, DateTime utcNow)
    {
        EnsureUsable(utcNow);
        if (MinOrderAmount is not null && subtotal < MinOrderAmount)
            throw new DomainException($"Мінімальна сума замовлення для промокоду: {MinOrderAmount:0.00}.");

        var discount = DiscountType == DiscountType.Percentage
            ? decimal.Round(subtotal * DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : DiscountValue;
        return Math.Min(discount, subtotal);
    }
    public void RegisterUse()
    {
        if (!IsActive || IsDeleted) throw new InvalidStateException("Промокод неактивний.");
        Usage.Increment(MaxUses);
    }

    private void EnsureUsable(DateTime utcNow)
    {
        if (!IsActive || IsDeleted)
            throw new InvalidStateException("Промокод неактивний.");
        if (utcNow < ValidFrom)
            throw new InvalidStateException("Промокод ще не діє.");
        if (ValidTo is not null && utcNow > ValidTo)
            throw new InvalidStateException("Термін дії промокоду минув.");
        if (MaxUses is not null && Usage.UsedCount >= MaxUses)
            throw new InvalidStateException("Ліміт використань промокоду вичерпано.");
    }

    private void SetPeriod(DateTime validFrom, DateTime? validTo)
    {
        if (validTo is not null && validTo <= validFrom)
            throw new DomainException("Кінець дії має бути пізніше за початок.");
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    private static void ValidateMaxUses(int? maxUses)
    {
        if (maxUses is <= 0)
            throw new DomainException("Ліміт використань має бути більше нуля.");
    }
}
