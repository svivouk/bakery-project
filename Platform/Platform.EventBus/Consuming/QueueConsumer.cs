using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus.Consuming;

public abstract class QueueConsumer(
    IConnection connection,
    EventMessageProcessor processor,
    ILogger logger,
    string queue,
    ushort prefetchCount = 10,
    int maxRetries = 3) : BackgroundService
{
    private IChannel? _channel;
    private string? _consumerTag;
    private IChannel? _retryChannel;
    private readonly SemaphoreSlim _retryLock = new(1, 1);
    protected virtual TimeSpan RetryDelay => TimeSpan.FromSeconds(1);

    protected string Queue { get; } = queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _channel = channel;

        await MessagingTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: prefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, message) => OnMessageAsync(channel, message, stoppingToken);

        _consumerTag = await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consumer started on queue {Queue} (prefetch {Prefetch})", Queue, prefetchCount);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs message, CancellationToken ct)
    {
        ProcessingResult result;
        try
        {
            result = await processor.ProcessAsync(message, Queue, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while processing message {MessageId}, sent to DLQ",
                message.BasicProperties.MessageId);
            result = MessageOutcome.Reject;
        }

        var outcome = result.Outcome;

        var messageId = message.BasicProperties.MessageId;
        try
        {
            if (outcome == MessageOutcome.Retry)
                outcome = await RetryOrDeadLetterAsync(message, result.Error!, ct);

            switch (outcome)
            {
                case MessageOutcome.Retry:
                    await channel.BasicAckAsync(message.DeliveryTag, multiple: false, CancellationToken.None);
                    logger.LogInformation("Ack original {MessageId} on {Queue} after scheduling retry", messageId, Queue);
                    break;
                case MessageOutcome.Ack:
                    await channel.BasicAckAsync(message.DeliveryTag, multiple: false, CancellationToken.None);
                    logger.LogInformation("Ack {MessageId} on {Queue} (delivery {DeliveryTag}, redelivered {Redelivered})",
                        messageId, Queue, message.DeliveryTag, message.Redelivered);
                    break;
                case MessageOutcome.Requeue:
                    await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
                    logger.LogWarning("Nack with requeue {MessageId} on {Queue} (delivery {DeliveryTag}, redelivered {Redelivered})",
                        messageId, Queue, message.DeliveryTag, message.Redelivered);
                    break;
                default:    
                    await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
                    logger.LogError("Nack without requeue {MessageId} on {Queue}: message goes to the dead letter exchange (delivery {DeliveryTag})",
                        messageId, Queue, message.DeliveryTag);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not {Outcome} message {MessageId}", outcome, messageId);
        }
    }
    private async Task<MessageOutcome> RetryOrDeadLetterAsync(
        BasicDeliverEventArgs message, Exception error, CancellationToken ct)
    {
        var props = message.BasicProperties;
        var attempt = MessageHeaders.GetInt(props, MessageHeaders.RetryCount);

        if (attempt >= maxRetries)
        {
            logger.LogError(error,
                "Message {MessageId} ({EventType}, correlation {CorrelationId}) on {Queue} failed after {Attempts} retries, sending to DLQ",
                props.MessageId, props.Type, MessageHeaders.GetString(props, MessageHeaders.CorrelationId), Queue, attempt);
            return MessageOutcome.Reject;                       
        }

        var next = attempt + 1;
        logger.LogWarning(
            "Message {MessageId} ({EventType}, correlation {CorrelationId}) on {Queue} retry {Attempt}/{Max}: {Reason}",
            props.MessageId, props.Type, MessageHeaders.GetString(props, MessageHeaders.CorrelationId),
            Queue, next, maxRetries, error.Message);

        try
        {
            await Task.Delay(RetryDelay * next, ct);

            var headers = props.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(props.Headers);
            headers[MessageHeaders.RetryCount] = next;
            headers[MessageHeaders.LastError] = Truncate($"{error.GetType().Name}: {error.Message}", 300);

            var copy = new BasicProperties(props) { Headers = headers };  

            await _retryLock.WaitAsync(ct);
            try
            {
                _retryChannel ??= await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    ct);
                await _retryChannel.BasicPublishAsync(
                    exchange: string.Empty, routingKey: Queue, mandatory: true, basicProperties: copy,
                    body: message.Body, cancellationToken: ct);
            }
            finally { _retryLock.Release(); }

            return MessageOutcome.Retry;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return MessageOutcome.Requeue;                        
        }
        catch (Exception publishError)
        {
            logger.LogError(publishError, "Could not republish {MessageId} for retry, returning it to the queue",
                props.MessageId);
            _retryChannel = null;
            return MessageOutcome.Requeue;
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is null) return;
        try
        {
            if (_consumerTag is not null && _channel.IsOpen)
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            await _channel.DisposeAsync();
            if (_retryChannel is not null) await _retryChannel.DisposeAsync();
            logger.LogInformation("Consumer on queue {Queue} stopped", Queue);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Consumer on queue {Queue} did not stop cleanly", Queue);
        }
    }
}
