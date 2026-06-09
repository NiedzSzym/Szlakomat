using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using DataProviders.Shared.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataProviders.EventsProvider;

public sealed class EventsConsumerService : RpcConsumerBase
{
    private readonly MockDataStore _mockData;
    private readonly string        _city;

    public EventsConsumerService(
        ILogger<EventsConsumerService> logger,
        IConfiguration config,
        MockDataStore mockData) : base(logger, config)
    {
        _mockData = mockData;
        _city     = (config["PROVIDER_CITY"] ?? "krakow").Trim().ToLowerInvariant();
    }

    protected override string RoutingKey => $"events.{_city}";

    protected override object HandleRequest(DataQuery query)
        => new EventsRequestHandler(_mockData, _city).Handle(query);
}
