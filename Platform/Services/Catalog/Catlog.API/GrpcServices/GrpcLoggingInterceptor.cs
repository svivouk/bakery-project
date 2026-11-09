using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Catalog.Api.GrpcServices;

/// <summary>Структуроване логування вхідних gRPC-викликів: метод, адреса клієнта (peer), статус, тривалість,
/// CorrelationId з метаданих. Додає атрибути до серверного span (rpc.* уже ставить ASP.NET Core instrumentation).
/// Реєструється першим, тому бачить уже перетворений <see cref="RpcException"/> зі статусом.</summary>
public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    private const string CorrelationHeader = "x-correlation-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = context.RequestHeaders.GetValue(CorrelationHeader);

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

            if (ex.StatusCode is StatusCode.Internal or StatusCode.Unknown)
            {
                // Стек уже записав DomainExceptionInterceptor: тут лише статус, щоб не дублювати.
                logger.LogError("gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                    context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds);
            }
            else
            {
                logger.LogWarning(ex, "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms ({Detail})",
                    context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds, ex.Status.Detail);
            }
            throw;
        }
    }
}
