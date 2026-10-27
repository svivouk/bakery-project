using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Models;

public sealed record OutboxMessage(
    Guid EventId,
    string EventType,          
    Guid AggregateId,
    object Payload,
    DateTime CreatedAt);

