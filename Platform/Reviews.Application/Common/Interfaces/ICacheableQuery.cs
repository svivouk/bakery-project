using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Interfaces;

public interface ICacheableQuery
{
    string? GetCacheKey();
    TimeSpan CacheDuration => TimeSpan.FromSeconds(30);
}