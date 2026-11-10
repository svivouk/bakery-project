using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Orders.Api.Middleware;

namespace Orders.Api.GrpcServices;

public sealed class GrpcServerInterceptor(ILogger<GrpcServerInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = context.RequestHeaders.GetValue("x-correlation-id");
        var activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("grpc.peer", context.Peer);

        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId });
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method, context.Peer, StatusCode.OK, sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
            logger.LogWarning(ex, "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method, context.Peer, StatusCode.Cancelled, sw.ElapsedMilliseconds);
            throw new RpcException(new Status(StatusCode.Cancelled, "Запит скасовано клієнтом."));
        }
        catch (Exception ex)
        {
            var error = ExceptionMapper.Map(ex);
            var code = error.Status switch
            {
                StatusCodes.Status400BadRequest => StatusCode.InvalidArgument,
                StatusCodes.Status404NotFound => StatusCode.NotFound,
                StatusCodes.Status409Conflict => StatusCode.FailedPrecondition,
                StatusCodes.Status503ServiceUnavailable => StatusCode.Unavailable,
                _ => StatusCode.Internal
            };

            activity?.SetTag("rpc.grpc.status_code", (int)code);
            logger.Log(code == StatusCode.Internal ? LogLevel.Error : LogLevel.Warning, ex,
                "gRPC {Method} from {Peer}: {ExceptionType} → {Status} in {Elapsed} ms",
                context.Method, context.Peer, ex.GetType().Name, code, sw.ElapsedMilliseconds);

            // Клієнту: безпечний опис (+ помилки валідації по полях), без стека й внутрішніх подробиць.
            var detail = error.ValidationErrors is null
                ? error.Detail
                : error.Detail + " " + string.Join("; ", error.ValidationErrors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"));
            throw new RpcException(new Status(code, detail));
        }
    }
}
