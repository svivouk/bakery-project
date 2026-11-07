using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll;

public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string what, object id) => new($"{what} '{id}' не знайдено.");
}
public sealed class ConflictException(string message) : Exception(message);
public sealed class InvalidQueryException(string message) : Exception(message);

