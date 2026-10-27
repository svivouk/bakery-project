IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE SEQUENCE [dbo].[OrderNumberSeq] START WITH 100001 INCREMENT BY 1 NO CYCLE;

CREATE TABLE [Customers] (
    [CustomerId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Name] nvarchar(150) NOT NULL,
    [Phone] varchar(20) NOT NULL,
    [Email] nvarchar(256) NULL,
    [AnonymizedAt] datetime2(0) NULL,
    [IsDeleted] AS CONVERT([bit], CASE WHEN [AnonymizedAt] IS NOT NULL THEN 1 ELSE 0 END) PERSISTED,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId]),
    CONSTRAINT [CK_Customers_Audit] CHECK ([UpdatedAt] >= [CreatedAt]),
    CONSTRAINT [CK_Customers_Email] CHECK ([Email] IS NULL OR [Email] LIKE '%_@_%._%'),
    CONSTRAINT [CK_Customers_Phone] CHECK ([AnonymizedAt] IS NOT NULL OR ([Phone] NOT LIKE '%[^0-9+]%' AND LEN([Phone]) BETWEEN 10 AND 15))
);

CREATE TABLE [InboxMessages] (
    [EventId] uniqueidentifier NOT NULL,
    [EventType] varchar(100) NOT NULL,
    [ProcessedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([EventId])
);

CREATE TABLE [OutboxMessages] (
    [OutboxId] bigint NOT NULL IDENTITY,
    [EventId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [EventType] varchar(100) NOT NULL,
    [AggregateId] uniqueidentifier NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [ProcessedAt] datetime2(3) NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([OutboxId]),
    CONSTRAINT [CK_OutboxMessages_Json] CHECK (ISJSON([Payload]) = 1)
);

CREATE TABLE [ProductRefs] (
    [ProductId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [SourceVersion] bigint NOT NULL,
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ProductRefs] PRIMARY KEY ([ProductId])
);

CREATE TABLE [PromoCodes] (
    [PromoCodeId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Code] nvarchar(50) NOT NULL,
    [DiscountType] tinyint NOT NULL,
    [DiscountValue] decimal(10,2) NOT NULL,
    [MinOrderAmount] decimal(10,2) NULL,
    [ValidFrom] datetime2(0) NOT NULL,
    [ValidTo] datetime2(0) NULL,
    [MaxUses] int NULL,
    [IsActive] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT [PK_PromoCodes] PRIMARY KEY ([PromoCodeId]),
    CONSTRAINT [CK_PromoCodes_Audit] CHECK ([UpdatedAt] >= [CreatedAt]),
    CONSTRAINT [CK_PromoCodes_Deleted] CHECK ([IsDeleted] = 0 OR [IsActive] = 0),
    CONSTRAINT [CK_PromoCodes_MaxUses] CHECK ([MaxUses] IS NULL OR [MaxUses] > 0),
    CONSTRAINT [CK_PromoCodes_Period] CHECK ([ValidTo] IS NULL OR [ValidTo] > [ValidFrom]),
    CONSTRAINT [CK_PromoCodes_Type] CHECK ([DiscountType] IN (1, 2)),
    CONSTRAINT [CK_PromoCodes_Value] CHECK (([DiscountType] = 1 AND [DiscountValue] BETWEEN 0 AND 100) OR ([DiscountType] = 2 AND [DiscountValue] >= 0))
);

CREATE TABLE [Addresses] (
    [AddressId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [CustomerId] uniqueidentifier NOT NULL,
    [Label] nvarchar(50) NULL,
    [City] nvarchar(100) NOT NULL,
    [Street] nvarchar(150) NOT NULL,
    [Building] nvarchar(20) NOT NULL,
    [Apartment] nvarchar(20) NULL,
    [Comment] nvarchar(200) NULL,
    [IsDefault] bit NOT NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([AddressId]),
    CONSTRAINT [FK_Addresses_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE CASCADE
);

CREATE TABLE [Carts] (
    [CartId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [CustomerId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Carts] PRIMARY KEY ([CartId]),
    CONSTRAINT [CK_Carts_Audit] CHECK ([UpdatedAt] >= [CreatedAt]),
    CONSTRAINT [FK_Carts_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE CASCADE
);

CREATE TABLE [VariantRefs] (
    [VariantId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Price] decimal(10,2) NOT NULL,
    [IsActive] bit NOT NULL,
    [SourceVersion] bigint NOT NULL,
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_VariantRefs] PRIMARY KEY ([VariantId]),
    CONSTRAINT [CK_VariantRefs_Price] CHECK ([Price] >= 0),
    CONSTRAINT [FK_VariantRefs_Product] FOREIGN KEY ([ProductId]) REFERENCES [ProductRefs] ([ProductId])
);

CREATE TABLE [Orders] (
    [OrderId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [OrderNumber] bigint NOT NULL DEFAULT (NEXT VALUE FOR dbo.OrderNumberSeq),
    [CustomerId] uniqueidentifier NOT NULL,
    [Status] varchar(30) NOT NULL,
    [DesiredReadyAt] datetime2(0) NOT NULL,
    [Comment] nvarchar(500) NULL,
    [PromoCodeId] uniqueidentifier NULL,
    [Subtotal] decimal(12,2) NOT NULL,
    [DiscountAmount] decimal(12,2) NOT NULL,
    [DeliveryFee] decimal(12,2) NOT NULL,
    [TotalAmount] AS [Subtotal] - [DiscountAmount] + [DeliveryFee] PERSISTED,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
    CONSTRAINT [CK_Orders_Amounts] CHECK ([Subtotal] >= 0 AND [DeliveryFee] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Subtotal]),
    CONSTRAINT [CK_Orders_Audit] CHECK ([UpdatedAt] >= [CreatedAt]),
    CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN ('New','Confirmed','Preparing','Ready','Completed','Cancelled')),
    CONSTRAINT [FK_Orders_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]),
    CONSTRAINT [FK_Orders_Promo] FOREIGN KEY ([PromoCodeId]) REFERENCES [PromoCodes] ([PromoCodeId])
);

CREATE TABLE [PromoCodeUsages] (
    [PromoCodeId] uniqueidentifier NOT NULL,
    [UsedCount] int NOT NULL,
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PromoCodeUsages] PRIMARY KEY ([PromoCodeId]),
    CONSTRAINT [CK_PromoCodeUsages_Count] CHECK ([UsedCount] >= 0),
    CONSTRAINT [FK_PromoCodeUsages_Promo] FOREIGN KEY ([PromoCodeId]) REFERENCES [PromoCodes] ([PromoCodeId]) ON DELETE CASCADE
);

CREATE TABLE [CartItems] (
    [CartItemId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [CartId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [VariantId] uniqueidentifier NOT NULL,
    [Quantity] int NOT NULL,
    [AddedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([CartItemId]),
    CONSTRAINT [CK_CartItems_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [FK_CartItems_Cart] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([CartId]) ON DELETE CASCADE
);

CREATE TABLE [Deliveries] (
    [OrderId] uniqueidentifier NOT NULL,
    [Type] varchar(20) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [City] nvarchar(100) NULL,
    [Street] nvarchar(150) NULL,
    [Building] nvarchar(20) NULL,
    [Apartment] nvarchar(20) NULL,
    [AddressNote] nvarchar(200) NULL,
    [CourierName] nvarchar(150) NULL,
    [CourierPhone] varchar(20) NULL,
    [DeliveredAt] datetime2(0) NULL,
    CONSTRAINT [PK_Deliveries] PRIMARY KEY ([OrderId]),
    CONSTRAINT [CK_Deliveries_Address] CHECK ([Type] = 'Pickup' OR ([City] IS NOT NULL AND [Street] IS NOT NULL AND [Building] IS NOT NULL)),
    CONSTRAINT [CK_Deliveries_Status] CHECK ([Status] IN ('Pending','Assigned','OutForDelivery','Delivered','Failed','Cancelled')),
    CONSTRAINT [CK_Deliveries_Type] CHECK ([Type] IN ('Delivery','Pickup')),
    CONSTRAINT [FK_Deliveries_Order] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE
);

CREATE TABLE [OrderItems] (
    [OrderItemId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [OrderId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [VariantId] uniqueidentifier NOT NULL,
    [ProductName] nvarchar(200) NOT NULL,
    [VariantName] nvarchar(100) NOT NULL,
    [UnitPrice] decimal(10,2) NOT NULL,
    [Quantity] int NOT NULL,
    [LineTotal] AS [UnitPrice] * [Quantity] PERSISTED,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY NONCLUSTERED ([OrderItemId]),
    CONSTRAINT [CK_OrderItems_Price] CHECK ([UnitPrice] >= 0),
    CONSTRAINT [CK_OrderItems_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [FK_OrderItems_Order] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE
);

CREATE TABLE [Payments] (
    [PaymentId] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [OrderId] uniqueidentifier NOT NULL,
    [Method] varchar(30) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [RefundedAmount] decimal(12,2) NOT NULL,
    [Currency] char(3) NOT NULL,
    [ExternalTransactionId] varchar(100) NULL,
    [PaidAt] datetime2(0) NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    [UpdatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedBy] nvarchar(128) NOT NULL DEFAULT (SUSER_SNAME()),
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0 AND [RefundedAmount] >= 0 AND [RefundedAmount] <= [Amount]),
    CONSTRAINT [CK_Payments_Audit] CHECK ([UpdatedAt] >= [CreatedAt]),
    CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('CardOnline','CardOnDelivery','Cash')),
    CONSTRAINT [CK_Payments_State] CHECK (([Status] IN ('Pending','Failed') AND [PaidAt] IS NULL AND [RefundedAmount] = 0) OR ([Status] = 'Paid' AND [PaidAt] IS NOT NULL AND [RefundedAmount] = 0) OR ([Status] = 'PartiallyRefunded' AND [PaidAt] IS NOT NULL AND [RefundedAmount] > 0 AND [RefundedAmount] < [Amount]) OR ([Status] = 'Refunded' AND [PaidAt] IS NOT NULL AND [RefundedAmount] = [Amount])),
    CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending','Paid','Failed','Refunded','PartiallyRefunded')),
    CONSTRAINT [FK_Payments_Order] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId])
);

CREATE UNIQUE INDEX [UX_Addresses_Default] ON [Addresses] ([CustomerId]) WHERE [IsDefault] = 1;

CREATE UNIQUE INDEX [UQ_CartItems_Variant] ON [CartItems] ([CartId], [VariantId]);

CREATE UNIQUE INDEX [UQ_Carts_Customer] ON [Carts] ([CustomerId]);

CREATE UNIQUE INDEX [UQ_Customers_Phone] ON [Customers] ([Phone]);

CREATE UNIQUE INDEX [UX_Customers_Email] ON [Customers] ([Email]) WHERE [Email] IS NOT NULL;

CREATE UNIQUE CLUSTERED INDEX [CX_OrderItems_Order] ON [OrderItems] ([OrderId], [OrderItemId]);

CREATE INDEX [IX_Orders_Customer_Date] ON [Orders] ([CustomerId], [CreatedAt] DESC) INCLUDE ([OrderNumber], [Status], [DesiredReadyAt], [TotalAmount]);

CREATE INDEX [IX_Orders_PromoCodeId] ON [Orders] ([PromoCodeId]);

CREATE INDEX [IX_Orders_Status_Ready] ON [Orders] ([Status], [DesiredReadyAt]) INCLUDE ([OrderNumber], [CustomerId]);

CREATE UNIQUE INDEX [UQ_Orders_Number] ON [Orders] ([OrderNumber]);

CREATE INDEX [IX_Outbox_Unprocessed] ON [OutboxMessages] ([OutboxId]) WHERE [ProcessedAt] IS NULL;

CREATE UNIQUE INDEX [UQ_OutboxMessages_Event] ON [OutboxMessages] ([EventId]);

CREATE INDEX [IX_Payments_Order] ON [Payments] ([OrderId], [CreatedAt] DESC) INCLUDE ([Method], [Status]);

CREATE UNIQUE INDEX [UX_Payments_External] ON [Payments] ([ExternalTransactionId]) WHERE [ExternalTransactionId] IS NOT NULL;

CREATE UNIQUE INDEX [UX_Payments_OnePaid] ON [Payments] ([OrderId]) WHERE [Status] = 'Paid';

CREATE UNIQUE INDEX [UX_PromoCodes_Code] ON [PromoCodes] ([Code]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_VariantRefs_Product] ON [VariantRefs] ([ProductId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025122459_InitialCreate', N'10.0.13');

COMMIT;
GO

