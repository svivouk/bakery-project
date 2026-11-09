using Catalog.Bll.Caching;
using Catalog.Domain.Exceptions;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Catalog.Api.GrpcServices;

/// <summary>Переводить доменні винятки в <see cref="RpcException"/> з відповідним <see cref="StatusCode"/>
/// (аналог ExceptionHandlingMiddleware для REST). Без цього клієнт отримав би загальний Unknown/Internal.
/// Також вмикає облік кешу для gRPC (CacheCallContext) і віддає x-cache у trailers.</summary>
public sealed class DomainExceptionInterceptor(ILogger<DomainExceptionInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Усе, що кеш зробить під час цього виклику, рахується як gRPC (окремо від HTTP).
        var cache = CacheCallContext.Begin("grpc");

        try
        {
            var response = await continuation(request, context);

            // Ознака кешу для клієнта в trailers: x-cache = hit | miss | none.
            context.ResponseTrailers.Add("x-cache", cache.Result);
            context.ResponseTrailers.Add("x-cache-hits", cache.Hits.ToString());
            context.ResponseTrailers.Add("x-cache-misses", cache.Misses.ToString());
            System.Diagnostics.Activity.Current?.SetTag("cache.result", cache.Result);
            return response;
        }
        catch (RpcException)
        {
            throw;   // уже має правильний статус (наприклад, InvalidArgument з валідації)
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Запит скасовано клієнтом."));
        }
        catch (DomainException ex)
        {
            var code = ex switch
            {
                NotFoundException => StatusCode.NotFound,
                ValidationException => StatusCode.InvalidArgument,
                InsufficientStockException => StatusCode.FailedPrecondition,
                ConcurrencyException => StatusCode.Aborted,
                ConflictException => StatusCode.AlreadyExists,
                _ => StatusCode.InvalidArgument
            };

            // Статус і тривалість логує GrpcLoggingInterceptor; тут лише деталі перетворення.
            logger.LogDebug("gRPC {Method}: {ExceptionType} → {StatusCode}: {Message}",
                context.Method, ex.GetType().Name, code, ex.Message);
            throw new RpcException(new Status(code, ex.Message));
        }
        catch (Exception ex)
        {
            // Подробиці (стек, SQL) клієнту не віддаємо.
            logger.LogError(ex, "gRPC {Method}: необроблена помилка", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Внутрішня помилка сервера."));
        }
    }
}
