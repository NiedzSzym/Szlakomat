using DataProviders.Shared.Transport;

namespace DataProviders.EventsProvider;

// TODO: zastąpić realną implementacją RabbitMQ — wzorzec analogiczny do PricingConsumerService
public sealed class EventsConsumerService : BackgroundService
{
    private readonly ILogger<EventsConsumerService> _logger;
    private readonly string _city;

    public EventsConsumerService(ILogger<EventsConsumerService> logger, IConfiguration config)
    {
        _logger = logger;
        _city   = (config["PROVIDER_CITY"] ?? "krakow").Trim().ToLowerInvariant();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var routingKey = $"events.{_city}";
        _logger.LogInformation(
            "EventsProvider [{RoutingKey}] szkielet uruchomiony. Exchange={Exchange}. Połączenie z RabbitMQ niezaimplementowane.",
            routingKey, RabbitMqConstants.Exchange);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
