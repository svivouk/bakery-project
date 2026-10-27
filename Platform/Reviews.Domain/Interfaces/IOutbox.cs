using Reviews.Domain.Models;

namespace Reviews.Domain.Interfaces;

public interface IOutbox
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default);
}