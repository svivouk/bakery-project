using MongoDB.Bson;
using Reviews.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Models;
internal static class ReviewIds
{
    public static bool IsValid(string? value) => ObjectId.TryParse(value, out var id) && id != ObjectId.Empty;

    public static ObjectId Parse(string? value) =>
        IsValid(value) ? ObjectId.Parse(value) : throw new DomainException("Некоректний ідентифікатор відгуку.");
}