using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Interfaces;

public interface IInbox
{
    Task<bool> TryRegisterAsync(Guid eventId, string eventType, CancellationToken ct = default);
}