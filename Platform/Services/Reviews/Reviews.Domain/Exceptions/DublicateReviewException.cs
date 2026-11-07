namespace Reviews.Domain.Exceptions;
public sealed class DuplicateReviewException : DomainException
{
    public Guid OrderItemId { get; }

    public DuplicateReviewException(Guid orderItemId)
        : base($"Відгук на покупку {orderItemId} уже існує.") => OrderItemId = orderItemId;
}