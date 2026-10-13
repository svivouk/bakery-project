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

/* ---------- Зміна статусу з перевіркою state machine ------------------ */

/* Статуси резерву (schema.sql): 0 = Reserved, 1 = Committed, 2 = Released.
   Дозволені переходи: 0 → 1, 0 → 2. Статуси 1 і 2 термінальні (назад шляху немає).
   Поточний статус читається під UPDLOCK + HOLDLOCK, тож між перевіркою і зміною
   жодна інша транзакція не може змінити рядок. Недопустимий перехід → THROW 50422,
   рядок не знайдено → THROW 50404. Повторний виклик з тим самим статусом — no-op (ідемпотентність).
   Перехід у 2 повертає кількість на склад (як usp_Stock_Release). */
CREATE OR ALTER PROCEDURE dbo.usp_StockReservation_ChangeStatus
    @OrderId   UNIQUEIDENTIFIER,
    @VariantId UNIQUEIDENTIFIER,
    @NewStatus TINYINT,
    @Actor     NVARCHAR(128) = N'system:orders'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Current TINYINT, @Qty INT;

    SELECT @Current = Status, @Qty = Quantity
    FROM   dbo.StockReservations WITH (UPDLOCK, HOLDLOCK)
    WHERE  OrderId = @OrderId AND VariantId = @VariantId;

    IF @Current IS NULL
    BEGIN
        ROLLBACK;
        THROW 50404, N'Резервування не знайдено.', 1;
    END

    IF @Current = @NewStatus
    BEGIN
        COMMIT;                       -- вже в потрібному статусі
        RETURN;
    END

    -- таблиця дозволених переходів (єдине місце, де описана state machine)
    IF NOT EXISTS (
        SELECT 1
        FROM (VALUES (0, 1), (0, 2)) AS t(FromStatus, ToStatus)
        WHERE t.FromStatus = @Current AND t.ToStatus = @NewStatus
    )
    BEGIN
        ROLLBACK;
        DECLARE @Msg NVARCHAR(200) =
            CONCAT(N'Недопустимий перехід статусу резерву: ', @Current, N' → ', @NewStatus, N'.');
        THROW 50422, @Msg, 1;
    END

    UPDATE dbo.StockReservations
    SET    Status = @NewStatus
    WHERE  OrderId = @OrderId AND VariantId = @VariantId;

    IF @NewStatus = 2
        UPDATE dbo.StockItems
        SET    QuantityAvailable = QuantityAvailable + @Qty,
               UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE  VariantId = @VariantId;

    COMMIT;
END
GO

/* Статуси товару (schema.sql): 0 = Hidden, 1 = Active. Дозволені переходи: 0 → 1 і 1 → 0.
   Видалений товар (IsDeleted = 1) статус не змінює: він «не знайдений» (50404).
   Рядок читається під UPDLOCK + HOLDLOCK: версію перевіряємо й статус змінюємо без втручання
   інших транзакцій. Чужа зміна → 50409, недопустимий перехід → 50422.
   Приховування (→ 0) публікує ProductDeactivated; для показу (→ 1) окремого типу події
   в схемі немає, тому подія не пишеться. Повертає нову RowVersion. */
CREATE OR ALTER PROCEDURE dbo.usp_Product_ChangeStatus
    @ProductId  UNIQUEIDENTIFIER,
    @NewStatus  TINYINT,
    @RowVersion BINARY(8),
    @Actor      NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Current TINYINT, @CurrentVersion BINARY(8);

    SELECT @Current = Status, @CurrentVersion = RowVersion
    FROM   dbo.Products WITH (UPDLOCK, HOLDLOCK)
    WHERE  ProductId = @ProductId AND IsDeleted = 0;

    IF @Current IS NULL
    BEGIN
        ROLLBACK;
        THROW 50404, N'Товар не знайдено або вже видалений.', 1;
    END

    IF @CurrentVersion <> @RowVersion
    BEGIN
        ROLLBACK;
        THROW 50409, N'Конфлікт: товар змінено іншим користувачем. Перечитайте дані.', 1;
    END

    IF @Current = @NewStatus
    BEGIN
        COMMIT;                       -- вже в потрібному статусі, зміни немає
        SELECT @CurrentVersion AS RowVersion;
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM (VALUES (0, 1), (1, 0)) AS t(FromStatus, ToStatus)
        WHERE t.FromStatus = @Current AND t.ToStatus = @NewStatus
    )
    BEGIN
        ROLLBACK;
        DECLARE @Msg NVARCHAR(200) =
            CONCAT(N'Недопустимий перехід статусу товару: ', @Current, N' → ', @NewStatus, N'.');
        THROW 50422, @Msg, 1;
    END

    UPDATE dbo.Products
    SET    Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE  ProductId = @ProductId;

    IF @NewStatus = 0
        INSERT INTO dbo.OutboxMessages (EventType, AggregateId, Payload)
        SELECT 'ProductDeactivated', p.ProductId,
               (SELECT p.ProductId AS productId, CONVERT(BIGINT, p.RowVersion) AS version
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
        FROM   dbo.Products p
        WHERE  p.ProductId = @ProductId;

    SELECT RowVersion FROM dbo.Products WHERE ProductId = @ProductId;

    COMMIT;
END
GO
