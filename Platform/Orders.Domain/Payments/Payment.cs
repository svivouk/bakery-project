using System.Text.RegularExpressions;
using Orders.Domain;
namespace Orders.Domain.Payments;
public enum PaymentMethod
{
    CardOnline,
    CardOnDelivery,
    Cash
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded
}

///  Окремий агрегат «Платіж»: на замовлення може бути кілька спроб, але лише одна Paid
/// (унікальний фільтрований індекс UX_Payments_OnePaid). Інваріанти CK_Payments_State
/// підтримуються методами переходів, а не відкритими setter-ами. 
public sealed partial class Payment : AuditableEntity
{
    public const int ExternalTransactionIdMaxLength = 100;
    public const string DefaultCurrency = "UAH";

    private Payment() { } // для EF Core

    public Payment(Guid orderId, PaymentMethod method, decimal amount, string currency = DefaultCurrency)
    {
        OrderId = Guard.NotEmpty(orderId, "Замовлення");
        if (!Enum.IsDefined(method))
            throw new DomainException("Невідомий спосіб оплати.");
        Method = method;

        if (amount <= 0)
            throw new DomainException("Сума платежу має бути більше нуля.");
        Amount = Guard.Money(amount, "Сума платежу", precision: 12);

        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        if (!CurrencyPattern().IsMatch(code))
            throw new DomainException("Валюта: очікується код із трьох літер (ISO 4217).");
        Currency = code;

        Status = PaymentStatus.Pending;
    }

    public Guid OrderId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string Currency { get; private set; } = DefaultCurrency;

    ///  Ідентифікатор у платіжному шлюзі; унікальний, коли заданий. 
    public string? ExternalTransactionId { get; private set; }

    public DateTime? PaidAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public decimal RefundableAmount => Amount - RefundedAmount;

    public void AttachExternalTransaction(string externalTransactionId)
    {
        RequireStatus(PaymentStatus.Pending);
        ExternalTransactionId = Guard.Required(externalTransactionId, "Ідентифікатор транзакції", ExternalTransactionIdMaxLength);
    }

    public void MarkPaid(DateTime utcNow)
    {
        RequireStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Paid;
        PaidAt = utcNow;
    }

    public void MarkFailed()
    {
        RequireStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Failed;
    }
    public void Refund(decimal amount)
    {
        RequireStatus(PaymentStatus.Paid, PaymentStatus.PartiallyRefunded);
        if (amount <= 0)
            throw new DomainException("Сума повернення має бути більше нуля.");
        Guard.Money(amount, "Сума повернення", precision: 12);
        if (amount > RefundableAmount)
            throw new DomainException($"Сума повернення перевищує доступну: {RefundableAmount:0.00}.");

        RefundedAmount += amount;
        Status = RefundedAmount == Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }

    private void RequireStatus(params PaymentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException($"Дію не можна виконати для платежу у статусі {Status}.");
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
