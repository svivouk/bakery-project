using System.Data;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

public sealed class CategoryRepository : RepositoryBase, ICategoryRepository
{
    private const string Columns =
        "CategoryId, Name, Slug, Description, SortOrder, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy";

    public CategoryRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dbo.Categories WHERE CategoryId = @Id AND IsDeleted = 0",
            p => p.Add("@Id", SqlDbType.UniqueIdentifier).Value = id, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dbo.Categories WHERE Slug = @Slug AND IsDeleted = 0",
            p => p.Add("@Slug", SqlDbType.VarChar, 120).Value = slug, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(bool activeOnly, CancellationToken ct = default)
    {
        var sql = $"SELECT {Columns} FROM dbo.Categories WHERE IsDeleted = 0"
                  + (activeOnly ? " AND IsActive = 1" : "")
                  + " ORDER BY SortOrder, Name";
        return await QueryAsync(sql, null, ct);
    }

    public Task<Guid> AddAsync(Category category, CancellationToken ct = default) => GuardAsync(async () =>
    {
        const string sql = @"
            INSERT INTO dbo.Categories (Name, Slug, Description, SortOrder, IsActive)
            OUTPUT INSERTED.CategoryId
            VALUES (@Name, @Slug, @Description, @SortOrder, @IsActive)";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        BindWritable(command.Parameters, category);
        var id = await command.ExecuteScalarAsync(ct);
        return (Guid)id!;
    });

    public Task<bool> UpdateAsync(Category category, CancellationToken ct = default) => GuardAsync(async () =>
    {
        const string sql = @"
            UPDATE dbo.Categories
            SET Name = @Name, Slug = @Slug, Description = @Description,
                SortOrder = @SortOrder, IsActive = @IsActive,
                UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
            WHERE CategoryId = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        BindWritable(command.Parameters, category);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = category.CategoryId;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    });

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => GuardAsync(async () =>
    {
        // CK_Categories_Deleted: IsDeleted = 1 вимагає IsActive = 0.
        const string sql = @"
            UPDATE dbo.Categories
            SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
            WHERE CategoryId = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    });

    public async Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @Id AND IsDeleted = 0)
                        THEN 1 ELSE 0 END";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = categoryId;
        return (int)(await command.ExecuteScalarAsync(ct))! == 1;
    }

    private async Task<List<Category>> QueryAsync(string sql, Action<SqlParameterCollection>? bind,
        CancellationToken ct)
    {
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        bind?.Invoke(command.Parameters);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<Category>();
        while (await reader.ReadAsync(ct))
            result.Add(Map(reader));
        return result;
    }
    private static Category Map(SqlDataReader r) => new()
    {
        CategoryId = r.GetGuid(0),
        Name = r.GetString(1),
        Slug = r.GetString(2),
        Description = r.IsDBNull(3) ? null : r.GetString(3),
        SortOrder = r.GetInt32(4),
        IsActive = r.GetBoolean(5),
        CreatedAt = r.GetDateTime(6),
        CreatedBy = r.GetString(7),
        UpdatedAt = r.GetDateTime(8),
        UpdatedBy = r.GetString(9)
    };

    private static void BindWritable(SqlParameterCollection p, Category c)
    {
        p.Add("@Name", SqlDbType.NVarChar, 100).Value = c.Name;
        p.Add("@Slug", SqlDbType.VarChar, 120).Value = c.Slug;
        p.Add("@Description", SqlDbType.NVarChar, 500).Value = (object?)c.Description ?? DBNull.Value;
        p.Add("@SortOrder", SqlDbType.Int).Value = c.SortOrder;
        p.Add("@IsActive", SqlDbType.Bit).Value = c.IsActive;
    }
}
