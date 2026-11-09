using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Bll.Caching;

public static class CachePolicies
{
    public static readonly DistributedCacheEntryOptions Reference = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    };
    public static readonly DistributedCacheEntryOptions ProductCard = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    };
    public static readonly DistributedCacheEntryOptions Listing = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
    };

    public static readonly DistributedCacheEntryOptions TopRated = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
    };
}
