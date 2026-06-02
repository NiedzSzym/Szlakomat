using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;
using Szlakomat.Products.Infrastructure;
using Szlakomat.Products.Infrastructure.DataGateway;

namespace Szlakomat.Products.Application.Tests.DataGateway;

/// <summary>
/// Journey: developer sends a data query through the gateway and observes routing outcomes.
/// </summary>
public class DataGatewayJourneyTests
{
    private readonly IMediator _mediator;
    private static readonly JsonElement EmptyPayload = JsonDocument.Parse("{}").RootElement;

    private static readonly string[] KnownProviders =
        ["pricing.krakow", "events.krakow", "pricing.warszawa", "events.warszawa"];

    public DataGatewayJourneyTests()
    {
        var services = new ServiceCollection();
        services.AddProductModule();
        services.AddDataGateway(opts => opts.KnownProviders = [.. KnownProviders]);
        _mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ValidKnownProvider_ReturnsNotImplementedWithRoutingKeyInMessage()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("pricing", "krakow", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("NOT_IMPLEMENTED", result.GetFailure()!.Code);
        Assert.Contains("pricing.krakow", result.GetFailure()!.Message);
    }

    [Fact]
    public async Task CaseInsensitiveInput_NormalizesToKnownProvider_ReturnsNotImplemented()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("Pricing", "KRAKOW", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("NOT_IMPLEMENTED", result.GetFailure()!.Code);
        Assert.Contains("pricing.krakow", result.GetFailure()!.Message);
    }

    [Fact]
    public async Task EmptyType_ReturnsValidationError()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("", "krakow", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("VALIDATION_ERROR", result.GetFailure()!.Code);
    }

    [Fact]
    public async Task WhitespaceCity_ReturnsValidationError()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("pricing", "   ", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("VALIDATION_ERROR", result.GetFailure()!.Code);
    }

    [Fact]
    public async Task UnknownCity_ReturnsProviderNotFound()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("pricing", "gdansk", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("PROVIDER_NOT_FOUND", result.GetFailure()!.Code);
        Assert.Contains("pricing.gdansk", result.GetFailure()!.Message);
    }

    [Fact]
    public async Task UnknownType_ReturnsProviderNotFound()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetAttractionData("hotels", "krakow", EmptyPayload));

        // Assert
        Assert.True(result.IsFailure());
        Assert.Equal("PROVIDER_NOT_FOUND", result.GetFailure()!.Code);
    }
}
