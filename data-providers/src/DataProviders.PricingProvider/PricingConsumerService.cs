using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using DataProviders.Shared.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataProviders.PricingProvider;

public sealed class PricingConsumerService : RpcConsumerBase
{
    private readonly MockDataStore _mockData;
    private readonly string        _city;

    public PricingConsumerService(
        ILogger<PricingConsumerService> logger,
        IConfiguration config,
        MockDataStore mockData) : base(logger, config)
    {
        _mockData = mockData;
        _city     = (config["PROVIDER_CITY"] ?? "krakow").Trim().ToLowerInvariant();
    }

    protected override string RoutingKey => $"pricing.{_city}";

    protected override object HandleRequest(DataQuery query)
        => new PricingRequestHandler(_mockData, _city).Handle(query);
}
