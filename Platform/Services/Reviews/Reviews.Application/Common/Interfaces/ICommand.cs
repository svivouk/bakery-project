using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Interfaces;
public interface ICommand: IRequest { }

public interface ICommand<TResponce>: IRequest<TResponce> { }