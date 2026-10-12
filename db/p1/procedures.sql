/* 
   catalog_db — збережувані процедури 
 */

USE catalog_db;
GO

/* Атомарне резервування. Результат: Reserved = 1 (успіх / повторний виклик) або 0 (не вистачає). */
CREATE OR ALTER PROCEDURE dbo.usp_Stock_Reserve
    @OrderId   UNIQUEIDENTIFIER,
    @VariantId UNIQUEIDENTIFIER,
    @Quantity  INT,
    @ExpiresAt DATETIME2(0) = NULL,
    @Actor     NVARCHAR(128) = N'system:orders'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    -- ідемпотентність: повторний виклик для того ж замовлення не списує вдруге
    IF EXISTS (SELECT 1 FROM dbo.StockReservations WITH (UPDLOCK, HOLDLOCK)
               WHERE OrderId = @OrderId AND VariantId = @VariantId)
    BEGIN
        COMMIT;
        SELECT CAST(1 AS BIT) AS Reserved;
        RETURN;
    END

    UPDATE dbo.StockItems
    SET    QuantityAvailable = QuantityAvailable - @Quantity,
           UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  VariantId = @VariantId
      AND  QuantityAvailable >= @Quantity;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        SELECT CAST(0 AS BIT) AS Reserved;
        RETURN;
    END

    INSERT INTO dbo.StockReservations (VariantId, OrderId, Quantity, ExpiresAt)
    VALUES (@VariantId, @OrderId, @Quantity, @ExpiresAt);

    COMMIT;
    SELECT CAST(1 AS BIT) AS Reserved;
END
GO

/* Скасування резерву (замовлення скасовано або резерв прострочено). */
CREATE OR ALTER PROCEDURE dbo.usp_Stock_Release
    @OrderId   UNIQUEIDENTIFIER,
    @VariantId UNIQUEIDENTIFIER,
    @Actor     NVARCHAR(128) = N'system:orders'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Qty INT;
    SELECT @Qty = Quantity
    FROM   dbo.StockReservations WITH (UPDLOCK, HOLDLOCK)
    WHERE  OrderId = @OrderId AND VariantId = @VariantId AND Status = 0;

    IF @Qty IS NULL
    BEGIN
        COMMIT;
        RETURN;                       -- нічого повертати (вже скасовано або підтверджено)
    END

    UPDATE dbo.StockReservations SET Status = 2
    WHERE  OrderId = @OrderId AND VariantId = @VariantId;

    UPDATE dbo.StockItems
    SET    QuantityAvailable = QuantityAvailable + @Qty,
           UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  VariantId = @VariantId;

    COMMIT;
END
GO

/* ---------- Оптимістична конкурентність у процедурах ----------------- */

/* Клієнт читає товар разом із RowVersion, відправляє його назад. Якщо рядок змінили в проміжку,
   UPDATE не зачепить жодного рядка → 50409 (API відповідає HTTP 409), а не мовчки затирає чужі зміни.
   Нове значення RowVersion повертається клієнту для наступного збереження. */
CREATE OR ALTER PROCEDURE dbo.usp_ProductVariant_UpdatePrice
    @VariantId  UNIQUEIDENTIFIER,
    @NewPrice   DECIMAL(10,2),
    @RowVersion BINARY(8),
    @Actor      NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.ProductVariants
    SET    Price = @NewPrice, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  VariantId = @VariantId AND IsDeleted = 0 AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        IF EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE VariantId = @VariantId AND IsDeleted = 0)
            THROW 50409, N'Конфлікт: запис змінено іншим користувачем. Перечитайте дані.', 1;
        THROW 50404, N'Варіант не знайдено.', 1;
    END

    -- версія події = RowVersion рядка: споживач ігнорує подію, якщо вже має новішу
    INSERT INTO dbo.OutboxMessages (EventType, AggregateId, Payload)
    SELECT 'PriceChanged', v.VariantId,
           (SELECT v.ProductId AS productId, v.VariantId AS variantId, v.Price AS price,
                   CONVERT(BIGINT, v.RowVersion) AS version
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM   dbo.ProductVariants v
    WHERE  v.VariantId = @VariantId;

    SELECT RowVersion FROM dbo.ProductVariants WHERE VariantId = @VariantId;

    COMMIT;
END
GO

/* М'яке видалення товару разом із його варіантами в одній транзакції. */
CREATE OR ALTER PROCEDURE dbo.usp_Product_SoftDelete
    @ProductId  UNIQUEIDENTIFIER,
    @RowVersion BINARY(8),
    @Actor      NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.Products
    SET    IsDeleted = 1, Status = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  ProductId = @ProductId AND IsDeleted = 0 AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        IF EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = @ProductId AND IsDeleted = 0)
            THROW 50409, N'Конфлікт: товар змінено іншим користувачем. Перечитайте дані.', 1;
        THROW 50404, N'Товар не знайдено або вже видалений.', 1;
    END

    UPDATE dbo.ProductVariants
    SET    IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  ProductId = @ProductId AND IsDeleted = 0;

    INSERT INTO dbo.OutboxMessages (EventType, AggregateId, Payload)
    SELECT 'ProductDeactivated', p.ProductId,
           (SELECT p.ProductId AS productId, CONVERT(BIGINT, p.RowVersion) AS version
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM   dbo.Products p
    WHERE  p.ProductId = @ProductId;

    COMMIT;
END
GO
