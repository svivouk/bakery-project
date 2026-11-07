using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

public sealed class VariantRepository : BaseDapperRepository<ProductVariant>, IVariantRepository
{
    public VariantRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.ProductVariants", "VariantId", softDelete: true) { }

    protected override string InsertSql => @"
        INSERT INTO dbo.ProductVariants (ProductId, Name, Sku, Price, WeightGrams, Servings, SortOrder, IsActive)
        OUTPUT INSERTED.VariantId
        VALUES (@ProductId, @Name, @Sku, @Price, @WeightGrams, @Servings, @SortOrder, @IsActive)";

    protected override string UpdateSql => @"
        UPDATE dbo.ProductVariants
        SET Name = @Name, Sku = @Sku, Price = @Price, WeightGrams = @WeightGrams, Servings = @Servings,
            SortOrder = @SortOrder, IsActive = @IsActive,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
        WHERE VariantId = @VariantId AND RowVersion = @RowVersion AND IsDeleted = 0";

    // Варіант не видаляють фізично (NO ACTION у FK), а вимикають; CK_ProductVariants_Deleted вимагає IsActive = 0.
    protected override string DeleteSql => @"
        UPDATE dbo.ProductVariants
        SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
        WHERE VariantId = @Id AND IsDeleted = 0";

    public async Task<IReadOnlyList<ProductVariant>> GetByProductAsync(Guid productId, bool includeInactive,
        CancellationToken ct = default)
    {
        // IsDeleted = 0 обов'язковий: його вимагає фільтрований індекс UX_ProductVariants_Name.
        var sql = @"
            SELECT * FROM dbo.ProductVariants
            WHERE ProductId = @ProductId AND IsDeleted = 0"
                  + (includeInactive ? "" : " AND IsActive = 1")
                  + " ORDER BY SortOrder, Name";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<ProductVariant>(
            new CommandDefinition(sql, new { ProductId = productId }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }
}
