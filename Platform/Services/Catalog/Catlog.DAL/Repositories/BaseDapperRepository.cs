using Catalog.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private readonly string _tableName;
    private readonly string _keyColumn;
    private readonly bool _softDelete;
    protected BaseDapperRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction,
        string tableName, string keyColumn, bool softDelete)
        : base(connection, currentTransaction)
    {
        _tableName = tableName;
        _keyColumn = keyColumn;
        _softDelete = softDelete;
    }

    protected abstract string InsertSql { get; }

    protected abstract string UpdateSql { get; }

    protected abstract string DeleteSql { get; }

    private string NotDeleted => _softDelete ? " AND IsDeleted = 0" : "";

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {_tableName} WHERE {_keyColumn} = @Id{NotDeleted}";
        return await Connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {_tableName}" + (_softDelete ? " WHERE IsDeleted = 0" : "");
        var rows = await Connection.QueryAsync<T>(
            new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public Task<Guid> AddAsync(T entity, CancellationToken ct = default) => GuardAsync(async () =>
    {
        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(InsertSql, entity, Transaction, cancellationToken: ct));
    });

    public Task<bool> UpdateAsync(T entity, CancellationToken ct = default) => GuardAsync(async () =>
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.ExecuteAsync(
            new CommandDefinition(UpdateSql, entity, Transaction, cancellationToken: ct));
        return rows > 0;
    });

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => GuardAsync(async () =>
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.ExecuteAsync(
            new CommandDefinition(DeleteSql, new { Id = id }, Transaction, cancellationToken: ct));
        return rows > 0;
    });
}
