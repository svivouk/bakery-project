using Orders.Domain;
using Orders.Domain.Promotions;

namespace Orders.Domain.Orders;

///  Корінь агрегату «Замовлення»: позиції (1:N), доставка (1:1) і правила життєвого циклу.
/// Суми рахує сам корінь — зовні їх змінити не можна. 
public sealed class Order : AuditableEntity
{
    public const int CommentMaxLength = 500;

    // Допустимі переходи статусу. У схемі перелічено лише самі статуси, тож ці правила — припущення
    // про процес: вперед по ланцюжку або скасування до готовності; Completed і Cancelled кінцеві.
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.New] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Preparing, OrderStatus.Cancelled],
            [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
            [OrderStatus.Ready] = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    private readonly List<OrderItem> _items = new();

    private Order() { } // для EF Core

    public Order(Guid customerId, DateTime desiredReadyAt, DateTime utcNow,
        IEnumerable<OrderLine> lines, Delivery delivery, decimal deliveryFee = 0m, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(delivery);

        CustomerId = Guard.NotEmpty(customerId, "Клієнт");
        if (desiredReadyAt <= utcNow)
            throw new DomainException("Бажаний час отримання має бути в майбутньому.");
        DesiredReadyAt = desiredReadyAt;

        foreach (var line in lines) _items.Add(new OrderItem(line));
        if (_items.Count == 0)
            throw new DomainException("Замовлення має містити хоча б одну позицію.");

        Delivery = delivery;
        DeliveryFee = Guard.Money(deliveryFee, "Вартість доставки", precision: 12);
        Comment = Guard.Optional(comment, "Коментар", CommentMaxLength);
        Status = OrderStatus.New;
        Recalculate();
    }

    ///  Номер для клієнта: генерується послідовністю dbo.OrderNumberSeq під час вставки. 
    public long OrderNumber { get; private set; }

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime DesiredReadyAt { get; private set; }
    public string? Comment { get; private set; }
    public Guid? PromoCodeId { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal DeliveryFee { get; private set; }

    ///  Обчислювана колонка БД (Subtotal - DiscountAmount + DeliveryFee); у пам'яті рахується одразу. 
    public decimal TotalAmount { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<OrderItem> Items => _items;
    public Delivery? Delivery { get; private set; }

    ///  Застосовує промокод до нового замовлення. Лічильник використань збільшує прикладний
    /// сервіс викликом <see cref="PromoCode.RegisterUse"/> у тій самій транзакції (це інший агрегат). 
    public void ApplyPromo(PromoCode promo, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(promo);
        EnsureStatus(OrderStatus.New, "Промокод можна застосувати лише до нового замовлення.");
        if (PromoCodeId is not null)
            throw new DomainException("Промокод уже застосовано.");

        DiscountAmount = promo.CalculateDiscount(Subtotal, utcNow);
        PromoCodeId = Guard.NotEmpty(promo.Id, "Промокод");
        Recalculate();
    }

    public void RemovePromo()
    {
        EnsureStatus(OrderStatus.New, "Промокод можна зняти лише з нового замовлення.");
        PromoCodeId = null;
        DiscountAmount = 0m;
        Recalculate();
    }

    public void ChangeComment(string? comment)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
            throw new DomainException("Завершене замовлення змінювати не можна.");
        Comment = Guard.Optional(comment, "Коментар", CommentMaxLength);
    }

    public void Confirm() => MoveTo(OrderStatus.Confirmed);
    public void StartPreparing() => MoveTo(OrderStatus.Preparing);
    public void MarkReady() => MoveTo(OrderStatus.Ready);
    public void Complete() => MoveTo(OrderStatus.Completed);

    ///  Скасовує замовлення разом із доставкою, якщо вона ще не вирушила. 
    public void Cancel()
    {
        if (!CanMoveTo(OrderStatus.Cancelled))
            throw InvalidTransition(OrderStatus.Cancelled);
        Delivery?.Cancel();
        Status = OrderStatus.Cancelled;
    }

    public bool CanMoveTo(OrderStatus next) => Transitions[Status].Contains(next);

    private void MoveTo(OrderStatus next)
    {
        if (!CanMoveTo(next)) throw InvalidTransition(next);
        Status = next;
    }

    private DomainException InvalidTransition(OrderStatus next)
        => new($"Недопустимий перехід статусу замовлення: {Status} → {next}.");

    private void EnsureStatus(OrderStatus expected, string message)
    {
        if (Status != expected) throw new DomainException(message);
    }

    private void Recalculate()
    {
        Subtotal = Guard.Money(_items.Sum(i => i.LineTotal), "Сума замовлення", precision: 12);
        if (DiscountAmount > Subtotal)
            throw new DomainException("Знижка не може перевищувати суму замовлення.");
        TotalAmount = Subtotal - DiscountAmount + DeliveryFee;
    }
}
