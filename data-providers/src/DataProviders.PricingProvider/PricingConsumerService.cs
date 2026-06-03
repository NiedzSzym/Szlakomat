using System.Text;
using System.Text.Json;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using DataProviders.Shared.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProviders.PricingProvider;

public sealed class PricingConsumerService : BackgroundService
{
    private readonly ILogger<PricingConsumerService> _logger;
    private readonly MockDataStore _mockData;
    private readonly string _city;
    private readonly string _rabbitHost;
    private IConnection? _connection;
    private IChannel?    _channel;

    public PricingConsumerService(
        ILogger<PricingConsumerService> logger,
        IConfiguration config,
        MockDataStore mockData)
    {
        _logger     = logger;
        _mockData   = mockData;
        _city       = (config["PROVIDER_CITY"] ?? "krakow").Trim().ToLowerInvariant();
        _rabbitHost = config["RABBITMQ_HOST"] ?? "localhost";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var routingKey = $"pricing.{_city}";
        var queueName  = routingKey;

        _logger.LogInformation("PricingProvider [{RoutingKey}] startuje. RabbitMQ host={Host}",
            routingKey, _rabbitHost);

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
            exchange:    RabbitMqConstants.Exchange,
            type:        RabbitMqConstants.ExchangeType,
            durable:     true,
            autoDelete:  false,
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
            DataQuery? query = null;
            try
            {
                query = JsonSerializer.Deserialize<DataQuery>(ea.Body.Span);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Błąd deserializacji wiadomości");
            }

            if (query is not null && !string.IsNullOrEmpty(replyTo))
            {
                var handler  = new PricingRequestHandler(_mockData, _city);
                var response = handler.Handle(query);

                var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response);

                var replyProps = new BasicProperties
                {
                    CorrelationId = corrId,
                    ContentType   = "application/json"
                };

                await _channel!.BasicPublishAsync(
                    exchange:        string.Empty,
                    routingKey:      replyTo,
                    mandatory:       false,
                    basicProperties: replyProps,
                    body:            responseBytes);

                _logger.LogInformation(
                    "Odpowiedź odesłana na '{ReplyTo}'. CorrelationId={CorrId}",
                    replyTo, corrId);
            }
            else if (string.IsNullOrEmpty(replyTo))
            {
                _logger.LogWarning("Wiadomość bez ReplyTo — pomijam odpowiedź. CorrelationId={CorrId}", corrId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd obsługi wiadomości. CorrelationId={CorrId}", corrId);
        }
        finally
        {
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel    is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
