using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Models;

public sealed record ReviewEligibility(
    Guid CustomerId,
    Guid ProductId,
    Guid OrderItemId,
    Guid OrderId,
    DateTime EligibleSince,
    ObjectId? ReviewId)
{
    public bool IsUsed => ReviewId.HasValue;
}

