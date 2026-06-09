using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Application.DataGateway;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Application.Tests.DataGateway;

/// <summary>
/// Testy HTTP kształtu błędu na poziomie API (bez RabbitMQ, bez I/O).
/// Weryfikują, że KAŻDA ścieżka błędu zwraca { status:"error", error:{ code, message } }.
/// </summary>
public class ApiErrorShapeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiErrorShapeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // ── zły JSON → 400 VALIDATION_ERROR ─────────────────────────────────────

    [Fact]
    public async Task MalformedJson_Returns400_WithValidationErrorShape()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{ not valid json !!!", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/data/query", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ParseErrorBody(response);
        Assert.Equal("error", body.Status);
        Assert.Equal("VALIDATION_ERROR", body.Error.Code);
    }

    [Fact]
    public async Task WrongContentType_Returns400_WithValidationErrorShape()
    {
        var client = _factory.CreateClient();
        var content = new StringContent(
            """{"type":"pricing","city":"krakow","payload":{}}""",
            Encoding.UTF8,
            "text/plain");

        var response = await client.PostAsync("/api/data/query", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ParseErrorBody(response);
        Assert.Equal("error", body.Status);
        Assert.Equal("VALIDATION_ERROR", body.Error.Code);
    }

    // ── niekontrolowany wyjątek → 500 INTERNAL_ERROR ─────────────────────────

    [Fact]
    public async Task GatewayThrows_Returns500_WithInternalErrorShape()
    {
        var client = BuildClientWithThrowingGateway();
        var content = new StringContent(
            """{"type":"pricing","city":"krakow","payload":{}}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/data/query", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await ParseErrorBody(response);
        Assert.Equal("error", body.Status);
        Assert.Equal("INTERNAL_ERROR", body.Error.Code);
    }

    [Fact]
    public async Task GatewayThrows_DoesNotLeakStackTrace()
    {
        var client = BuildClientWithThrowingGateway();
        var content = new StringContent(
            """{"type":"pricing","city":"krakow","payload":{}}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/data/query", content);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("StackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Szlakomat", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── helper: klient z bramą rzucającą wyjątek ─────────────────────────────

    private HttpClient BuildClientWithThrowingGateway()
        => _factory.WithWebHostBuilder(wb =>
            wb.ConfigureServices(services =>
            {
                // Ustaw znanych providerów — żeby walidacja handlera przepuściła request
                var optsDesc = services.SingleOrDefault(d => d.ServiceType == typeof(DataGatewayOptions));
                if (optsDesc is not null) services.Remove(optsDesc);
                services.AddSingleton(new DataGatewayOptions
                {
                    KnownProviders = ["pricing.krakow"]
                });

                // Zastąp gateway wersją rzucającą wyjątek
                services.AddSingleton<IDataGateway>(new ThrowingDataGateway());
            })).CreateClient();

    // ── helper: deserializacja kształtu błędu ────────────────────────────────

    private static async Task<(string Status, (string Code, string Message) Error)> ParseErrorBody(
        HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root    = doc.RootElement;
        var status  = root.GetProperty("status").GetString() ?? "";
        var errEl   = root.GetProperty("error");
        var code    = errEl.GetProperty("code").GetString() ?? "";
        var message = errEl.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";
        return (status, (code, message));
    }

    // ── fake rzucający wyjątek ───────────────────────────────────────────────

    private sealed class ThrowingDataGateway : IDataGateway
    {
        public Task<Result<ErrorInfo, QueryResponse>> Query(
            GetAttractionData request, string routingKey, CancellationToken ct)
            => throw new InvalidOperationException("Symulowany błąd infrastruktury");
    }
}
