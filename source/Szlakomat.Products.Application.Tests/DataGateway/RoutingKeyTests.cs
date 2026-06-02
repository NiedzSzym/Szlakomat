using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Application.Tests.DataGateway;

public class RoutingKeyTests
{
    [Fact]
    public void From_NormalizesUppercaseTypeAndCity()
    {
        Assert.Equal("pricing.krakow", RoutingKey.From("Pricing", "KRAKOW"));
    }

    [Fact]
    public void From_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.Equal("events.warszawa", RoutingKey.From("  events  ", "  warszawa  "));
    }

    [Fact]
    public void From_HandlesAlreadyLowercaseInput()
    {
        Assert.Equal("pricing.krakow", RoutingKey.From("pricing", "krakow"));
    }

    [Fact]
    public void From_CombinesTypeAndCityWithDot()
    {
        Assert.Equal("events.krakow", RoutingKey.From("EVENTS", "Krakow"));
    }
}
