using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Exceptions;

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' не знайдено.");