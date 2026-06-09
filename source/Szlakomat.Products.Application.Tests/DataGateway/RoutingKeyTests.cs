using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Application.Tests.DataGateway;

public class RoutingKeyTests
{
    [Fact]
    public void From_NormalizesUppercaseType()
    {
        Assert.Equal("pricing", RoutingKey.From("Pricing"));
    }

    [Fact]
    public void From_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.Equal("events", RoutingKey.From("  events  "));
    }

    [Fact]
    public void From_HandlesAlreadyLowercaseInput()
    {
        Assert.Equal("pricing", RoutingKey.From("pricing"));
    }

    [Fact]
    public void From_ReturnsTypeOnly()
    {
        Assert.Equal("events", RoutingKey.From("EVENTS"));
    }
}
