using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using DataProviders.Shared.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataProviders.PricingProvider;

public sealed class PricingConsumerService : RpcConsumerBase
{
    private readonly MockDataStore _mockData;

    public PricingConsumerService(
        ILogger<PricingConsumerService> logger,
        IConfiguration config,
        MockDataStore mockData) : base(logger, config)
    {
        _mockData = mockData;
    }

    protected override string RoutingKey => "pricing";

    protected override object HandleRequest(DataQuery query)
        => new PricingRequestHandler(_mockData).Handle(query);
}
