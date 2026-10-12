/* 
   catalog_db — Контекст «Каталог» (Проєкт №1: ADO.NET + Dapper)
   СУБД: SQL Server 2019+ (T-SQL)
   Порядок запуску: schema.sql → procedures.sql → seed.sql*/

IF DB_ID(N'catalog_db') IS NULL CREATE DATABASE catalog_db;
GO

USE catalog_db;
GO

/* ---------- Каталог ------------------------------------------------ */

CREATE TABLE dbo.Categories (
    CategoryId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name        NVARCHAR(100)    NOT NULL,
    Slug        VARCHAR(120)     NOT NULL, -- ім'я латинецею для URL
    Description NVARCHAR(500)    NULL,
    SortOrder   INT              NOT NULL DEFAULT 0,
    IsActive    BIT              NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    CONSTRAINT PK_Categories      PRIMARY KEY (CategoryId),
    CONSTRAINT CK_Categories_Slug CHECK (Slug NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_100_BIN2), 
    CONSTRAINT CK_Categories_Audit   CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_Categories_Deleted CHECK (IsDeleted = 0 OR IsActive = 0)
);
CREATE UNIQUE INDEX UX_Categories_Name ON dbo.Categories (Name) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Categories_Slug ON dbo.Categories (Slug) WHERE IsDeleted = 0;
GO

CREATE TABLE dbo.Ingredients (
    IngredientId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name         NVARCHAR(100)    NOT NULL,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    CONSTRAINT PK_Ingredients      PRIMARY KEY (IngredientId),
    CONSTRAINT CK_Ingredients_Audit   CHECK (UpdatedAt >= CreatedAt)
);
CREATE UNIQUE INDEX UX_Ingredients_Name ON dbo.Ingredients (Name) WHERE IsDeleted = 0;
GO

CREATE TABLE dbo.Allergens (
    AllergenId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Code       VARCHAR(30)      NOT NULL,   -- стабільний код: GLUTEN, MILK, ...
    Name       NVARCHAR(100)    NOT NULL,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT PK_Allergens      PRIMARY KEY (AllergenId),
    CONSTRAINT UQ_Allergens_Code UNIQUE (Code),
    CONSTRAINT UQ_Allergens_Name UNIQUE (Name),
    CONSTRAINT CK_Allergens_Audit   CHECK (UpdatedAt >= CreatedAt)
);
GO

/* ---------- Агрегат Product ------------------------------------------ */

CREATE TABLE dbo.Products (
    ProductId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CategoryId     UNIQUEIDENTIFIER NOT NULL,
    Name           NVARCHAR(200)    NOT NULL,
    Slug           VARCHAR(220)     NOT NULL,
    Description    NVARCHAR(2000)   NULL,
    ImageUrl       NVARCHAR(500)    NULL,
    BasePrice      DECIMAL(10,2)    NOT NULL,          -- ціна «від»
    WeightGrams    INT              NULL,
    ShelfLifeHours INT              NULL,              -- термін придатності
    Status         TINYINT          NOT NULL DEFAULT 1, -- 0 = Hidden, 1 = Active
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_Products          PRIMARY KEY (ProductId),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (CategoryId),
    CONSTRAINT CK_Products_Price    CHECK (BasePrice >= 0),
    CONSTRAINT CK_Products_Weight   CHECK (WeightGrams IS NULL OR WeightGrams > 0),
    CONSTRAINT CK_Products_Shelf    CHECK (ShelfLifeHours IS NULL OR ShelfLifeHours > 0),
    CONSTRAINT CK_Products_Status   CHECK (Status IN (0, 1)),
    CONSTRAINT CK_Products_Slug     CHECK (Slug NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_Products_Audit   CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_Products_Deleted CHECK (IsDeleted = 0 OR Status = 0)
);
CREATE UNIQUE INDEX UX_Products_Slug ON dbo.Products (Slug) WHERE IsDeleted = 0;
CREATE INDEX IX_Products_Active_Category ON dbo.Products (CategoryId)
    INCLUDE (Name, Slug, ImageUrl, BasePrice, CreatedAt)
    WHERE Status = 1;
GO

CREATE TABLE dbo.ProductRatings (
    ProductId     UNIQUEIDENTIFIER NOT NULL,
    AvgRating     DECIMAL(3,2)     NOT NULL DEFAULT 0,
    ReviewCount   INT              NOT NULL DEFAULT 0,
    RatingVersion BIGINT           NOT NULL DEFAULT 0,   -- версія події: застарілі події ігноруються
    UpdatedAt     DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProductRatings         PRIMARY KEY (ProductId),
    CONSTRAINT FK_ProductRatings_Product FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId) ON DELETE CASCADE,
    CONSTRAINT CK_ProductRatings_Rating  CHECK (AvgRating BETWEEN 0 AND 5 AND ReviewCount >= 0
                                                AND (ReviewCount > 0 OR AvgRating = 0))
);
/* Q2 «найкращі за рейтингом»: йдемо за індексом у порядку рейтингу й для кожного рядка перевіряємо
   товар за PK (Status = 1 AND IsDeleted = 0), доки не наберемо TOP (N). */
CREATE INDEX IX_ProductRatings_Rank ON dbo.ProductRatings (AvgRating DESC, ReviewCount DESC);
GO

CREATE TABLE dbo.ProductVariants (
    VariantId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,             -- «20 см», «1 кг», «6 порцій»
    Sku         VARCHAR(40)      NOT NULL,
    Price       DECIMAL(10,2)    NOT NULL,
    WeightGrams INT              NULL,
    Servings    INT              NULL,
    SortOrder   INT              NOT NULL DEFAULT 0,
    IsActive    BIT              NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_ProductVariants         PRIMARY KEY (VariantId),
    CONSTRAINT FK_ProductVariants_Product FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId),   -- NO ACTION: варіант не видаляють, а вимикають (IsActive = 0)
    CONSTRAINT CK_ProductVariants_Price   CHECK (Price >= 0),
    CONSTRAINT CK_ProductVariants_Weight  CHECK (WeightGrams IS NULL OR WeightGrams > 0),
    CONSTRAINT CK_ProductVariants_Serv    CHECK (Servings IS NULL OR Servings > 0),
    CONSTRAINT CK_ProductVariants_Audit   CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_ProductVariants_Deleted CHECK (IsDeleted = 0 OR IsActive = 0)
);
CREATE UNIQUE INDEX UX_ProductVariants_Sku  ON dbo.ProductVariants (Sku) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_ProductVariants_Name ON dbo.ProductVariants (ProductId, Name) WHERE IsDeleted = 0; -- також обслуговує вибірку варіантів товару (запит має містити IsDeleted = 0)
/* UX_ProductVariants_Name (ProductId, Name) уже обслуговує вибірку варіантів товару.
   агрегатів vw_ProductListing (MIN/MAX Price активних варіантів): фільтрований і покриваючий. */
CREATE INDEX IX_ProductVariants_ActivePrice ON dbo.ProductVariants (ProductId) INCLUDE (Price) WHERE IsActive = 1;
GO

CREATE TABLE dbo.ProductIngredients (
    ProductId    UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_ProductIngredients PRIMARY KEY (ProductId, IngredientId),
    CONSTRAINT FK_ProdIngr_Product   FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId) ON DELETE CASCADE,
    CONSTRAINT FK_ProdIngr_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES dbo.Ingredients (IngredientId)
);
GO

CREATE TABLE dbo.ProductAllergens (
    ProductId  UNIQUEIDENTIFIER NOT NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL,
    Presence   TINYINT          NOT NULL DEFAULT 1,    -- 1 = містить, 2 = може містити сліди
    CONSTRAINT PK_ProductAllergens   PRIMARY KEY (ProductId, AllergenId),
    CONSTRAINT FK_ProdAllerg_Product FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId) ON DELETE CASCADE,
    CONSTRAINT FK_ProdAllerg_Allergen FOREIGN KEY (AllergenId)
        REFERENCES dbo.Allergens (AllergenId),
    CONSTRAINT CK_ProdAllerg_Presence CHECK (Presence IN (1, 2))
);
GO

/* ---------- Агрегат StockItem ---------------------------------------- */

CREATE TABLE dbo.StockItems (
    VariantId         UNIQUEIDENTIFIER NOT NULL,
    QuantityAvailable INT              NOT NULL,       -- доступно для замовлення зараз
    DailyLimit        INT              NOT NULL DEFAULT 0, -- добова норма (скидається щоночі)
    -- RowVersion немає: рядок змінюється з кожним замовленням, усі зміни — атомарні UPDATE з умовою
    -- (див. usp_Stock_Reserve); оптимістичний токен давав би хибні конфлікти при редагуванні DailyLimit.
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT PK_StockItems         PRIMARY KEY (VariantId),
    CONSTRAINT FK_StockItems_Variant FOREIGN KEY (VariantId)
        REFERENCES dbo.ProductVariants (VariantId) ON DELETE CASCADE,
    CONSTRAINT CK_StockItems_Qty     CHECK (QuantityAvailable >= 0 AND DailyLimit >= 0),
    CONSTRAINT CK_StockItems_Audit   CHECK (UpdatedAt >= CreatedAt)
);
GO

CREATE TABLE dbo.StockReservations (
    ReservationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    VariantId     UNIQUEIDENTIFIER NOT NULL,
    OrderId       UNIQUEIDENTIFIER NOT NULL,
    Quantity      INT              NOT NULL,
    Status        TINYINT          NOT NULL DEFAULT 0, -- 0 = Reserved, 1 = Committed, 2 = Released
    CreatedAt     DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAt     DATETIME2(0)     NULL,
    CONSTRAINT PK_StockReservations         PRIMARY KEY (ReservationId),
    CONSTRAINT UQ_StockReservations_Order   UNIQUE (OrderId, VariantId),
    CONSTRAINT FK_StockReservations_Stock   FOREIGN KEY (VariantId) REFERENCES dbo.StockItems (VariantId),
    CONSTRAINT CK_StockReservations_Qty     CHECK (Quantity > 0),
    CONSTRAINT CK_StockReservations_Status  CHECK (Status IN (0, 1, 2))
);
CREATE INDEX IX_StockReservations_Expiry ON dbo.StockReservations (ExpiresAt)
    INCLUDE (OrderId, VariantId, Quantity) WHERE Status = 0;
GO

/* ---------- Події (Outbox / Inbox) ----------------------------------- */

/* Подія пишеться в ту саму транзакцію, що й зміна даних.
   Типи: ProductCreated, ProductRenamed, PriceChanged, ProductDeactivated, StockChanged */
CREATE TABLE dbo.OutboxMessages (
    OutboxId    BIGINT IDENTITY(1,1) NOT NULL,
    EventId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    EventType   VARCHAR(100)     NOT NULL,
    AggregateId UNIQUEIDENTIFIER NOT NULL,
    Payload     NVARCHAR(MAX)    NOT NULL,
    CreatedAt   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAt DATETIME2(3)     NULL,
    CONSTRAINT PK_OutboxMessages PRIMARY KEY (OutboxId),
    CONSTRAINT UQ_OutboxMessages_Event UNIQUE (EventId),
    CONSTRAINT CK_OutboxMessages_Json CHECK (ISJSON(Payload) = 1)
);
CREATE INDEX IX_Outbox_Unprocessed ON dbo.OutboxMessages (OutboxId) WHERE ProcessedAt IS NULL;
GO

/* Оброблені вхідні події (споживач: RatingSummaryChanged з контексту «Відгуки») */
CREATE TABLE dbo.InboxMessages (
    EventId     UNIQUEIDENTIFIER NOT NULL,
    EventType   VARCHAR(100)     NOT NULL,
    ProcessedAt DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_InboxMessages PRIMARY KEY (EventId)
);
GO

/* Денормалізована «картка товару» для списків і пошуку (одним запитом, без JOIN у коді).
   Якщо навантаження зросте — матеріалізувати в таблицю ProductListingCache,
   що оновлюється подіями (індексоване view тут неможливе: MIN/MAX/STRING_AGG та OUTER APPLY). */

CREATE OR ALTER VIEW dbo.vw_ProductListing
AS
SELECT  p.ProductId, p.Name, p.Slug, p.ImageUrl,   -- Description навмисно немає: потрібен лише на сторінці товару
        c.CategoryId, c.Name AS CategoryName, c.Slug AS CategorySlug,
        p.BasePrice,
        v.MinPrice, v.MaxPrice, v.VariantCount,
        ISNULL(s.TotalAvailable, 0) AS TotalAvailable,
        CAST(CASE WHEN ISNULL(s.TotalAvailable, 0) > 0 THEN 1 ELSE 0 END AS BIT) AS InStock,
        ISNULL(r.AvgRating, 0) AS AvgRating, ISNULL(r.ReviewCount, 0) AS ReviewCount,
        al.Allergens,
        p.CreatedAt
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
LEFT JOIN dbo.ProductRatings r ON r.ProductId = p.ProductId
OUTER APPLY (
    SELECT MIN(pv.Price) AS MinPrice, MAX(pv.Price) AS MaxPrice, COUNT(*) AS VariantCount
    FROM dbo.ProductVariants pv
    WHERE pv.ProductId = p.ProductId AND pv.IsActive = 1
) v
OUTER APPLY (
    SELECT SUM(si.QuantityAvailable) AS TotalAvailable
    FROM dbo.ProductVariants pv
    JOIN dbo.StockItems si ON si.VariantId = pv.VariantId
    WHERE pv.ProductId = p.ProductId AND pv.IsActive = 1
) s
OUTER APPLY (
    SELECT STRING_AGG(CAST(a.Name AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY a.Name) AS Allergens
    FROM dbo.ProductAllergens pa
    JOIN dbo.Allergens a ON a.AllergenId = pa.AllergenId
    WHERE pa.ProductId = p.ProductId AND pa.Presence = 1
) al
WHERE p.Status = 1 AND p.IsDeleted = 0 AND c.IsActive = 1 AND c.IsDeleted = 0;
GO
