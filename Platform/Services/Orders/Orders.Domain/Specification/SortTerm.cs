using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.Specification;

public sealed record SortTerm(string Field, bool Descending);
