using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Models;

public enum ReadConsistency
{
    Eventual,
    Strong
}