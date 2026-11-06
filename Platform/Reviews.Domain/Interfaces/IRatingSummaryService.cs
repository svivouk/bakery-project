using Reviews.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Interfaces;

public interface IRatingSummaryService
{
    Task OnApprovedAsync(Review review, CancellationToken ct = default);
    Task OnDeletedAsync(Review review, CancellationToken ct = default);
    Task ReconcileAsync(Guid productId, CancellationToken ct = default);
}
