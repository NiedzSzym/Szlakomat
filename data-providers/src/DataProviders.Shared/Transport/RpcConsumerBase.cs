using System.Text.Json;
using DataProviders.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProviders.Shared.Transport;

public abstract class RpcConsumerBase : BackgroundService
{
    private readonly ILogger _logger;
    private readonly string  _rabbitHost;
    private IConnection? _connection;
    private IChannel?    _channel;

    protected RpcConsumerBase(ILogger logger, IConfiguration config)
    {
        _logger     = logger;
        _rabbitHost = config["RABBITMQ_HOST"] ?? "localhost";
    }

    protected abstract string RoutingKey { get; }
    protected abstract object HandleRequest(DataQuery query);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var routingKey = RoutingKey;
        var queueName  = routingKey;

        _logger.LogInformation("{Type} [{RoutingKey}] startuje. RabbitMQ host={Host}",
            GetType().Name, routingKey, _rabbitHost);

        var factory = new ConnectionFactory { HostName = _rabbitHost };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie można połączyć z RabbitMQ. Ponowna próba za 5 s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        if (stoppingToken.IsCancellationRequested) return;

        _channel = await _connection!.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange:   RabbitMqConstants.Exchange,
            type:       RabbitMqConstants.ExchangeType,
            durable:    true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue:       queueName,
            durable:     true,
            exclusive:   false,
            autoDelete:  false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue:       queueName,
            exchange:    RabbitMqConstants.Exchange,
            routingKey:  routingKey,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Kolejka '{Queue}' zbindowana do exchange '{Exchange}' kluczem '{Key}'. Oczekiwanie na wiadomości...",
            queueName, RabbitMqConstants.Exchange, routingKey);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel.BasicConsumeAsync(
            queue:    queueName,
            autoAck:  false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        var props   = ea.BasicProperties;
        var replyTo = props.ReplyTo;
        var corrId  = props.CorrelationId;

        _logger.LogInformation("Odebrano zapytanie. CorrelationId={CorrId}, ReplyTo={ReplyTo}",
            corrId, replyTo);

        try
        {
            // ── 1. Deserializacja ───────────────────────────────────────────────
            // Niepoprawny JSON nie może zatrzymać konsumenta; wysyłamy błąd i ackujemy.
            DataQuery? query;
            try
            {
                query = JsonSerializer.Deserialize<DataQuery>(ea.Body.Span);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Niepoprawny JSON w wiadomości. CorrelationId={CorrId}", corrId);
                await TryReplyErrorAsync(replyTo, corrId,
                    "VALIDATION_ERROR", "Niepoprawny format komunikatu JSON");
                return;
            }

            if (query is null)
            {
                _logger.LogWarning("Puste ciało wiadomości. CorrelationId={CorrId}", corrId);
                await TryReplyErrorAsync(replyTo, corrId,
                    "VALIDATION_ERROR", "Puste ciało komunikatu");
                return;
            }

            if (string.IsNullOrEmpty(replyTo))
            {
                _logger.LogWarning(
                    "Wiadomość bez ReplyTo — pomijam odpowiedź. CorrelationId={CorrId}", corrId);
                return;
            }

            // ── 2. Logika biznesowa ─────────────────────────────────────────────
            // Wyjątek z handlera nie crasha konsumenta; wysyłamy INTERNAL_ERROR.
            object response;
            try
            {
                response = HandleRequest(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd logiki biznesowej. CorrelationId={CorrId}", corrId);
                await ReplyAsync(replyTo, corrId,
                    BuildError("INTERNAL_ERROR", "Błąd wewnętrzny providera"));
                return;
            }

            await ReplyAsync(replyTo, corrId, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nieoczekiwany błąd obsługi wiadomości. CorrelationId={CorrId}", corrId);
        }
        finally
        {
            // Zawsze ack — nigdy nie wpychamy wiadomości z powrotem do kolejki w pętli.
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
    }

    // ── helpery odpowiedzi ────────────────────────────────────────────────────

    private async Task TryReplyErrorAsync(string? replyTo, string? corrId, string code, string message)
    {
        if (string.IsNullOrEmpty(replyTo)) return;
        await ReplyAsync(replyTo, corrId, BuildError(code, message));
    }

    private async Task ReplyAsync(string replyTo, string? corrId, object payload)
    {
        var body  = JsonSerializer.SerializeToUtf8Bytes(payload);
        var props = new BasicProperties
        {
            CorrelationId = corrId,
            ContentType   = "application/json"
        };
        await _channel!.BasicPublishAsync(
            exchange:        string.Empty,
            routingKey:      replyTo,
            mandatory:       false,
            basicProperties: props,
            body:            body);
        _logger.LogInformation(
            "Odpowiedź odesłana na '{ReplyTo}'. CorrelationId={CorrId}", replyTo, corrId);
    }

    private static ErrorResponse BuildError(string code, string message)
        => new("error", new ErrorInfo(code, message));

    // ── zatrzymanie ───────────────────────────────────────────────────────────

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel    is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
