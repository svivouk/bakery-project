namespace Catalog.Domain.Constants;

public enum ProductStatus : byte
{
    Hidden = 0,
    Active = 1
}
public enum ReservationStatus : byte
{
    Reserved = 0,
    Committed = 1,
    Released = 2
}

public enum AllergenPresence : byte
{
    Contains = 1,
    MayContainTraces = 2
}

public enum ProductSort
{
    Newest = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    Rating = 3
}

public static class EventTypes
{
    public const string ProductCreated = "ProductCreated";
    public const string ProductRenamed = "ProductRenamed";
    public const string PriceChanged = "PriceChanged";
    public const string ProductDeactivated = "ProductDeactivated";
    public const string StockChanged = "StockChanged";
    public const string RatingSummaryChanged = "RatingSummaryChanged";
}
