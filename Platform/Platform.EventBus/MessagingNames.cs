namespace Platform.EventBus;

public static class Exchanges
{
    public const string CatalogEvents = "catalog-events";
    public const string CatalogDlx = "catalog-events.dlx";

    public const string OrdersEvents = "orders-events";
    public const string OrdersDlx = "orders-events.dlx";
}

public static class RoutingKeys
{
    public const string ProductCreated = "product.created";
    public const string ProductPriceChanged = "product.price_changed";
    public const string ProductRenamed = "product.renamed";
    public const string ProductDeleted = "product.deleted";

    public const string OrderStatusChanged = "order.status_changed";
}
public static class Queues
{
    public const string OrdersCatalogSync = "orders.catalog-sync";
    public const string ReviewsCatalogSync = "reviews.catalog-sync";
    public const string CatalogOrdersSync = "catalog.orders-sync";

    public static string DeadLetter(string queue) => queue + ".dlq";
}
