using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Common.Behaviors;
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;

        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("{RequestName} cancelled by caller", name);
            throw;
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorCode, e.ErrorMessage }).ToList();
            logger.LogWarning("{RequestName} failed validation: {@ValidationErrors}", name, errors);
            throw;
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            logger.LogWarning("{RequestName} rejected: {ErrorType}: {ErrorMessage}", name, ex.GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in {RequestName} {@Request}", name, request);
            throw;
        }
    }
}