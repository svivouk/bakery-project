using Reviews.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Models;

public enum ReviewSort { Newest, Oldest, RatingHigh, RatingLow }

public sealed record ReviewCriteria
{
    public Guid? ProductId { get; init; }
    public Guid? CustomerId { get; init; }         
    public ReviewStatus? Status { get; init; }
    public int? MinRating { get; init; }
    public int? MaxRating { get; init; }
    public DateTime? CreatedFrom { get; init; }      
    public DateTime? CreatedTo { get; init; }         
    public bool? HasPhotos { get; init; }             
    public bool? HasReply { get; init; }             
    public bool IncludeDeleted { get; init; }
    public ReviewSort Sort { get; init; } = ReviewSort.Newest;
}
