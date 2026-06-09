using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Application.DataGateway;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;
using Szlakomat.Products.Domain.Common;
using Szlakomat.Products.Infrastructure;

namespace Szlakomat.Products.Application.Tests.DataGateway;

/// <summary>
/// Journey: developer sends a data query through the gateway and observes routing outcomes.
/// FakeDataGateway is used — no real RabbitMQ connection required.
/// </summary>
public class DataGatewayJourneyTests
{
    private readonly IMediator _mediator;
    private static readonly JsonElement EmptyPayload = JsonDocument.Parse("{}").RootElement;

    private static readonly string[] KnownProviders = ["pricing", "events"];

    public DataGatewayJourneyTests()
    {
        var services = new ServiceCollection();
        services.AddProductModule();
        services.AddSingleton(new DataGatewayOptions { KnownProviders = [.. KnownProviders] });
        services.AddSingleton<IDataGateway>(new FakeDataGateway());
        _mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    // ── brama osiągalna ──────────────────────────────────────────────────────

    [Fact]
    public async Task ValidKnownProvider_ReturnsNotImplementedWithRoutingKeyInMessage()
    {
        var result = await _mediator.Send(new GetAttractionData("pricing", "krakow", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("NOT_IMPLEMENTED", result.GetFailure()!.Code);
        Assert.Contains("pricing", result.GetFailure()!.Message);
    }

    [Fact]
    public async Task CaseInsensitiveInput_NormalizesToKnownProvider_ReturnsNotImplemented()
    {
        var result = await _mediator.Send(new GetAttractionData("Pricing", "KRAKOW", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("NOT_IMPLEMENTED", result.GetFailure()!.Code);
        Assert.Contains("pricing", result.GetFailure()!.Message);
    }

    [Fact]
    public async Task AnyCity_WithKnownType_ReachesGateway()
    {
        // Miasto nie jest częścią routing key — każde miasto dociera do providera.
        // Walidacja istnienia miasta należy do data-providera, nie do bramy.
        var result = await _mediator.Send(new GetAttractionData("pricing", "gdansk", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("NOT_IMPLEMENTED", result.GetFailure()!.Code);
    }

    // ── walidacja handlera (nie dochodzi do bramy) ───────────────────────────

    [Fact]
    public async Task EmptyType_ReturnsValidationError()
    {
        var result = await _mediator.Send(new GetAttractionData("", "krakow", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("VALIDATION_ERROR", result.GetFailure()!.Code);
    }

    [Fact]
    public async Task WhitespaceCity_ReturnsValidationError()
    {
        var result = await _mediator.Send(new GetAttractionData("pricing", "   ", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("VALIDATION_ERROR", result.GetFailure()!.Code);
    }

    [Fact]
    public async Task UnknownType_ReturnsProviderNotFound()
    {
        var result = await _mediator.Send(new GetAttractionData("hotels", "krakow", EmptyPayload));

        Assert.True(result.IsFailure());
        Assert.Equal("PROVIDER_NOT_FOUND", result.GetFailure()!.Code);
    }

    // ── fake bez I/O ─────────────────────────────────────────────────────────

    private sealed class FakeDataGateway : IDataGateway
    {
        public Task<Result<ErrorInfo, QueryResponse>> Query(
            GetAttractionData request, string routingKey, CancellationToken ct)
            => Task.FromResult(Result<ErrorInfo, QueryResponse>.FailureOf(
                new ErrorInfo("NOT_IMPLEMENTED", $"Fake gateway -> {routingKey}")));
    }
}
