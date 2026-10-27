using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Interfaces;

public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}