using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Interfaces;

public interface IProductNameVersionStore {
    Task<bool> TryAdvanceAsync(Guid productId, long version, CancellationToken ct = default);
}