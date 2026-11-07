using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Interfaces;

public interface IQuery<TResponce> : IRequest<TResponce> { }