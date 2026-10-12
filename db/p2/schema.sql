IF DB_ID(N'orders_db') IS NULL CREATE DATABASE orders_db;
GO

USE orders_db;
GO

CREATE SEQUENCE dbo.OrderNumberSeq AS BIGINT START WITH 100001 INCREMENT BY 1;
GO

/* ---------- Агрегат Customer ----------------------------------------- */

CREATE TABLE dbo.Customers (
    CustomerId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name         NVARCHAR(150)    NOT NULL,
    Phone        VARCHAR(20)      NOT NULL,
    Email        NVARCHAR(256)    NULL,
    AnonymizedAt DATETIME2(0)     NULL,             -- замість видалення: замовлення мають зберегтися
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    -- анонімізація і є м'яким видаленням: один факт — одне джерело
    IsDeleted  AS (CAST(CASE WHEN AnonymizedAt IS NOT NULL THEN 1 ELSE 0 END AS BIT)) PERSISTED,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_Customers       PRIMARY KEY (CustomerId),
    CONSTRAINT UQ_Customers_Phone UNIQUE (Phone),
    CONSTRAINT CK_Customers_Phone CHECK (AnonymizedAt IS NOT NULL
        OR (Phone NOT LIKE '%[^0-9+]%' AND LEN(Phone) BETWEEN 10 AND 15)),
    CONSTRAINT CK_Customers_Email CHECK (Email IS NULL OR Email LIKE '%_@_%._%'),
    CONSTRAINT CK_Customers_Audit   CHECK (UpdatedAt >= CreatedAt)
);
CREATE UNIQUE INDEX UX_Customers_Email ON dbo.Customers (Email) WHERE Email IS NOT NULL;
GO

CREATE TABLE dbo.Addresses (
    AddressId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    Label      NVARCHAR(50)     NULL,               
    City       NVARCHAR(100)    NOT NULL,
    Street     NVARCHAR(150)    NOT NULL,
    Building   NVARCHAR(20)     NOT NULL,
    Apartment  NVARCHAR(20)     NULL,
    Comment    NVARCHAR(200)    NULL,              
    IsDefault  BIT              NOT NULL DEFAULT 0,
    CONSTRAINT PK_Addresses          PRIMARY KEY (AddressId),
    CONSTRAINT FK_Addresses_Customer FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId) ON DELETE CASCADE
);
CREATE INDEX IX_Addresses_Customer ON dbo.Addresses (CustomerId);
CREATE UNIQUE INDEX UX_Addresses_Default ON dbo.Addresses (CustomerId) WHERE IsDefault = 1; -- одна адреса за замовчуванням
GO

/* ---------- Агрегат PromoCode ---------------------------------------- */

CREATE TABLE dbo.PromoCodes (
    PromoCodeId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Code           NVARCHAR(50)     NOT NULL,
    DiscountType   TINYINT          NOT NULL,       -- 1 = відсоток, 2 = фіксована сума
    DiscountValue  DECIMAL(10,2)    NOT NULL,
    MinOrderAmount DECIMAL(10,2)    NULL,
    ValidFrom      DATETIME2(0)     NOT NULL,
    ValidTo        DATETIME2(0)     NULL,
    MaxUses        INT              NULL,           -- NULL = без ліміту
    IsActive       BIT              NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_PromoCodes      PRIMARY KEY (PromoCodeId),
    CONSTRAINT CK_PromoCodes_Type CHECK (DiscountType IN (1, 2)),
    CONSTRAINT CK_PromoCodes_Value CHECK (
        (DiscountType = 1 AND DiscountValue BETWEEN 0 AND 100) OR
        (DiscountType = 2 AND DiscountValue >= 0)),
    CONSTRAINT CK_PromoCodes_Period CHECK (ValidTo IS NULL OR ValidTo > ValidFrom),
    CONSTRAINT CK_PromoCodes_Audit   CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_PromoCodes_Deleted CHECK (IsDeleted = 0 OR IsActive = 0),
    CONSTRAINT CK_PromoCodes_MaxUses CHECK (MaxUses IS NULL OR MaxUses > 0)
);
CREATE UNIQUE INDEX UX_PromoCodes_Code ON dbo.PromoCodes (Code) WHERE IsDeleted = 0;
GO


CREATE TABLE dbo.PromoCodeUsages (
    PromoCodeId UNIQUEIDENTIFIER NOT NULL,
    UsedCount   INT              NOT NULL DEFAULT 0,
    UpdatedAt   DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PromoCodeUsages       PRIMARY KEY (PromoCodeId),
    CONSTRAINT FK_PromoCodeUsages_Promo FOREIGN KEY (PromoCodeId)
        REFERENCES dbo.PromoCodes (PromoCodeId) ON DELETE CASCADE,
    CONSTRAINT CK_PromoCodeUsages_Count CHECK (UsedCount >= 0)
);
GO

/* ---------- Агрегат Cart --------------------------------------------- */

CREATE TABLE dbo.Carts (
    CartId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt  DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION    NOT NULL,   -- зміна CartItems оновлює Carts.UpdatedAt, отже й токен (дві вкладки / пристрої)
    CONSTRAINT PK_Carts          PRIMARY KEY (CartId),
    CONSTRAINT UQ_Carts_Customer UNIQUE (CustomerId),       -- один активний кошик на клієнта
    CONSTRAINT FK_Carts_Customer FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId) ON DELETE CASCADE,
    CONSTRAINT CK_Carts_Audit    CHECK (UpdatedAt >= CreatedAt)
);
GO

CREATE TABLE dbo.CartItems (
    CartItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CartId     UNIQUEIDENTIFIER NOT NULL,
    ProductId  UNIQUEIDENTIFIER NOT NULL,
    VariantId  UNIQUEIDENTIFIER NOT NULL,
    Quantity   INT              NOT NULL,
    AddedAt    DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CartItems         PRIMARY KEY (CartItemId),
    CONSTRAINT UQ_CartItems_Variant UNIQUE (CartId, VariantId),
    CONSTRAINT FK_CartItems_Cart    FOREIGN KEY (CartId)
        REFERENCES dbo.Carts (CartId) ON DELETE CASCADE,
    CONSTRAINT CK_CartItems_Qty     CHECK (Quantity > 0)
);
GO

/* ---------- Агрегат Order -------------------------------------------- */

CREATE TABLE dbo.Orders (
    OrderId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    OrderNumber    BIGINT           NOT NULL DEFAULT (NEXT VALUE FOR dbo.OrderNumberSeq), -- номер для клієнта
    CustomerId     UNIQUEIDENTIFIER NOT NULL,
    Status         VARCHAR(30)      NOT NULL DEFAULT 'New',     -- EF: HasConversion<string>()
    DesiredReadyAt DATETIME2(0)     NOT NULL,                   -- бажаний час отримання
    Comment        NVARCHAR(500)    NULL,
    PromoCodeId    UNIQUEIDENTIFIER NULL,
    Subtotal       DECIMAL(12,2)    NOT NULL,                   -- сума позицій (рахує корінь агрегату)
    DiscountAmount DECIMAL(12,2)    NOT NULL DEFAULT 0,         
    DeliveryFee    DECIMAL(12,2)    NOT NULL DEFAULT 0,
    TotalAmount    AS (Subtotal - DiscountAmount + DeliveryFee) PERSISTED, 
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_Orders          PRIMARY KEY (OrderId),
    CONSTRAINT UQ_Orders_Number   UNIQUE (OrderNumber),
    CONSTRAINT FK_Orders_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT FK_Orders_Promo    FOREIGN KEY (PromoCodeId) REFERENCES dbo.PromoCodes (PromoCodeId),
    CONSTRAINT CK_Orders_Status   CHECK (Status IN ('New','Confirmed','Preparing','Ready','Completed','Cancelled')),
    CONSTRAINT CK_Orders_Amounts  CHECK (Subtotal >= 0 AND DeliveryFee >= 0
                                         AND DiscountAmount >= 0 AND DiscountAmount <= Subtotal),
    CONSTRAINT CK_Orders_Audit   CHECK (UpdatedAt >= CreatedAt)
);
CREATE INDEX IX_Orders_Customer_Date ON dbo.Orders (CustomerId, CreatedAt DESC)
    INCLUDE (OrderNumber, Status, DesiredReadyAt, TotalAmount);
CREATE INDEX IX_Orders_Status_Ready ON dbo.Orders (Status, DesiredReadyAt) INCLUDE (OrderNumber, CustomerId);
GO

CREATE TABLE dbo.OrderItems (
    OrderItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    OrderId     UNIQUEIDENTIFIER NOT NULL,
    ProductId   UNIQUEIDENTIFIER NOT NULL,              -- ідентифікатори Каталогу
    VariantId   UNIQUEIDENTIFIER NOT NULL,
    ProductName NVARCHAR(200)    NOT NULL,              -- знімок
    VariantName NVARCHAR(100)    NOT NULL,              -- знімок
    UnitPrice   DECIMAL(10,2)    NOT NULL,              -- знімок
    Quantity    INT              NOT NULL,
    LineTotal   AS (UnitPrice * Quantity) PERSISTED,
    CONSTRAINT PK_OrderItems       PRIMARY KEY NONCLUSTERED (OrderItemId),
    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,
    CONSTRAINT CK_OrderItems_Qty   CHECK (Quantity > 0),
    CONSTRAINT CK_OrderItems_Price CHECK (UnitPrice >= 0)
);
CREATE UNIQUE CLUSTERED INDEX CX_OrderItems_Order ON dbo.OrderItems (OrderId, OrderItemId);
GO

-- Доставка 1:1 із замовленням (EF Core: OwnsOne + ToTable("Deliveries")).
CREATE TABLE dbo.Deliveries (
    OrderId      UNIQUEIDENTIFIER NOT NULL,
    Type         VARCHAR(20)      NOT NULL,             -- Delivery | Pickup
    Status       VARCHAR(30)      NOT NULL DEFAULT 'Pending',
    City         NVARCHAR(100)    NULL,
    Street       NVARCHAR(150)    NULL,
    Building     NVARCHAR(20)     NULL,
    Apartment    NVARCHAR(20)     NULL,
    AddressNote  NVARCHAR(200)    NULL,
    CourierName  NVARCHAR(150)    NULL,
    CourierPhone VARCHAR(20)      NULL,
    DeliveredAt  DATETIME2(0)     NULL,
    CONSTRAINT PK_Deliveries       PRIMARY KEY (OrderId),
    CONSTRAINT FK_Deliveries_Order FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,
    CONSTRAINT CK_Deliveries_Type    CHECK (Type IN ('Delivery','Pickup')),
    CONSTRAINT CK_Deliveries_Status  CHECK (Status IN ('Pending','Assigned','OutForDelivery','Delivered','Failed','Cancelled')),
    CONSTRAINT CK_Deliveries_Address CHECK (Type = 'Pickup'
        OR (City IS NOT NULL AND Street IS NOT NULL AND Building IS NOT NULL))
);
GO

/* ---------- Агрегат Payment ------------------------------------------ */


CREATE TABLE dbo.Payments (
    PaymentId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    OrderId               UNIQUEIDENTIFIER NOT NULL,
    Method                VARCHAR(30)      NOT NULL,
    Status                VARCHAR(30)      NOT NULL DEFAULT 'Pending',
    Amount                DECIMAL(12,2)    NOT NULL,
    RefundedAmount        DECIMAL(12,2)    NOT NULL DEFAULT 0,
    Currency              CHAR(3)          NOT NULL DEFAULT 'UAH',
    ExternalTransactionId VARCHAR(100)     NULL,        -- id у платіжному шлюзі
    PaidAt                DATETIME2(0)     NULL,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    UpdatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy  NVARCHAR(128) NOT NULL DEFAULT (SUSER_SNAME()),
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_Payments        PRIMARY KEY (PaymentId),
    CONSTRAINT FK_Payments_Order  FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId),
    CONSTRAINT CK_Payments_Method CHECK (Method IN ('CardOnline','CardOnDelivery','Cash')),
    CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending','Paid','Failed','Refunded','PartiallyRefunded')),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0 AND RefundedAmount >= 0 AND RefundedAmount <= Amount),
    CONSTRAINT CK_Payments_State  CHECK (
        (Status IN ('Pending','Failed') AND PaidAt IS NULL     AND RefundedAmount = 0) OR
        (Status = 'Paid'                AND PaidAt IS NOT NULL AND RefundedAmount = 0) OR
        (Status = 'PartiallyRefunded'   AND PaidAt IS NOT NULL AND RefundedAmount > 0 AND RefundedAmount < Amount) OR
        (Status = 'Refunded'            AND PaidAt IS NOT NULL AND RefundedAmount = Amount)),
    CONSTRAINT CK_Payments_Audit   CHECK (UpdatedAt >= CreatedAt)
);
CREATE INDEX IX_Payments_Order ON dbo.Payments (OrderId, CreatedAt DESC) INCLUDE (Method, Status);
CREATE UNIQUE INDEX UX_Payments_External ON dbo.Payments (ExternalTransactionId)
    WHERE ExternalTransactionId IS NOT NULL;
CREATE UNIQUE INDEX UX_Payments_OnePaid ON dbo.Payments (OrderId) WHERE Status = 'Paid';
GO

/* ---------- Локальні копії Каталогу ------------------------ */

/* Оновлюються лише подіями з Каталогу (ProductRenamed, PriceChanged, ProductDeactivated).
   Розбито на дві таблиці, щоб Name товару не повторювався в кожному варіанті (3НФ).
   SourceVersion — версія події: застарілі події ігноруються. */
CREATE TABLE dbo.ProductRefs (
    ProductId     UNIQUEIDENTIFIER NOT NULL,
    Name          NVARCHAR(200)    NOT NULL,
    IsActive      BIT              NOT NULL,
    SourceVersion BIGINT           NOT NULL DEFAULT 0,
    UpdatedAt     DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProductRefs PRIMARY KEY (ProductId)
);
GO

CREATE TABLE dbo.VariantRefs (
    VariantId     UNIQUEIDENTIFIER NOT NULL,
    ProductId     UNIQUEIDENTIFIER NOT NULL,
    Name          NVARCHAR(100)    NOT NULL,
    Price         DECIMAL(10,2)    NOT NULL,
    IsActive      BIT              NOT NULL,
    SourceVersion BIGINT           NOT NULL DEFAULT 0,
    UpdatedAt     DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_VariantRefs         PRIMARY KEY (VariantId),
    CONSTRAINT FK_VariantRefs_Product FOREIGN KEY (ProductId) REFERENCES dbo.ProductRefs (ProductId),
    CONSTRAINT CK_VariantRefs_Price   CHECK (Price >= 0)
);
CREATE INDEX IX_VariantRefs_Product ON dbo.VariantRefs (ProductId);
GO

/* ---------- Події (Outbox / Inbox) ----------------------------------- */

CREATE TABLE dbo.OutboxMessages (
    OutboxId    BIGINT IDENTITY(1,1) NOT NULL,
    EventId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    EventType   VARCHAR(100)     NOT NULL,
    AggregateId UNIQUEIDENTIFIER NOT NULL,
    Payload     NVARCHAR(MAX)    NOT NULL,
    CreatedAt   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAt DATETIME2(3)     NULL,
    CONSTRAINT PK_OutboxMessages       PRIMARY KEY (OutboxId),
    CONSTRAINT UQ_OutboxMessages_Event UNIQUE (EventId),
    CONSTRAINT CK_OutboxMessages_Json  CHECK (ISJSON(Payload) = 1)
);
CREATE INDEX IX_Outbox_Unprocessed ON dbo.OutboxMessages (OutboxId) WHERE ProcessedAt IS NULL;
GO

CREATE TABLE dbo.InboxMessages (
    EventId     UNIQUEIDENTIFIER NOT NULL,
    EventType   VARCHAR(100)     NOT NULL,
    ProcessedAt DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_InboxMessages PRIMARY KEY (EventId)
);
GO

/* ---------- Представлення для читання -------------------------------- */

/* Кошик з актуальними назвами та цінами (з локальної копії довідника).
   Якщо копії немає або товар вимкнено — IsAvailable = 0. */
CREATE OR ALTER VIEW dbo.vw_CartView
AS
SELECT  c.CartId, c.CustomerId, ci.CartItemId, ci.ProductId, ci.VariantId,
        pr.Name AS ProductName, vr.Name AS VariantName,
        vr.Price AS UnitPrice, ci.Quantity,
        vr.Price * ci.Quantity AS LineTotal,
        CAST(CASE WHEN pr.IsActive = 1 AND vr.IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS IsAvailable
FROM dbo.Carts c
JOIN dbo.CartItems ci       ON ci.CartId = c.CartId
LEFT JOIN dbo.VariantRefs vr ON vr.VariantId = ci.VariantId
LEFT JOIN dbo.ProductRefs pr ON pr.ProductId = ci.ProductId;
GO

CREATE OR ALTER VIEW dbo.vw_OrderSummary
AS
SELECT  o.OrderId, o.OrderNumber, o.Status, o.CreatedAt, o.DesiredReadyAt,
        o.Subtotal, o.DiscountAmount, o.DeliveryFee, o.TotalAmount,
        cu.CustomerId, cu.Name AS CustomerName, cu.Phone AS CustomerPhone,
        d.Type AS DeliveryType, d.Status AS DeliveryStatus,
        d.City, d.Street, d.Building,
        pay.Method AS PaymentMethod, pay.Status AS PaymentStatus,
        ic.ItemCount, ic.TotalQuantity
FROM dbo.Orders o
JOIN dbo.Customers cu ON cu.CustomerId = o.CustomerId
LEFT JOIN dbo.Deliveries d ON d.OrderId = o.OrderId
OUTER APPLY (
    SELECT TOP (1) p.Method, p.Status
    FROM dbo.Payments p
    WHERE p.OrderId = o.OrderId
    ORDER BY p.CreatedAt DESC
) pay
OUTER APPLY (
    SELECT COUNT(*) AS ItemCount, SUM(oi.Quantity) AS TotalQuantity
    FROM dbo.OrderItems oi
    WHERE oi.OrderId = o.OrderId
) ic;
GO
