using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoUnitOfWork(
    MongoDbContext context,
    MongoTransactionSupport support,
    IOptions<MongoDbSettings> options) : IUnitOfWork
{    private static readonly TransactionOptions Transaction = new(
        readConcern: ReadConcern.Majority,
        readPreference: ReadPreference.Primary,
        writeConcern: WriteConcern.WMajority);

    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        if (context.Session is not null)
        {
            await work(ct);
            return;
        }

        if (!await UseTransactionAsync(ct))
        {
            await work(ct);          // без транзакції: атомарність лише на рівні одного документа + компенсації
            return;
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        context.Session = session;
        try
        {
            await session.WithTransactionAsync(async (_, token) =>
            {
                await work(token);
                return true;
            }, Transaction, ct);
        }
        finally
        {
            context.Session = null;
        }
    }

    private async Task<bool> UseTransactionAsync(CancellationToken ct) => options.Value.Transactions switch
    {
        TransactionMode.Disabled => false,
        TransactionMode.Enabled => true,
        _ => await support.IsSupportedAsync(ct)
    };
}