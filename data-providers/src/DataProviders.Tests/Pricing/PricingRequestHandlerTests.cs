using System.Text.Json;
using DataProviders.PricingProvider;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataProviders.Tests.Pricing;

public class PricingRequestHandlerTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    private static readonly string FixtureJson = """
        {
          "krakow": {
            "wawel-castle": {
              "name": "Zamek Królewski na Wawelu",
              "pricing": {
                "currency": "PLN",
                "prices": [
                  { "label": "Bilet normalny", "amount": 3500 },
                  { "label": "Bilet ulgowy",   "amount": 2200 }
                ]
              },
              "events": { "upcoming": [] }
            }
          }
        }
        """;

    private PricingRequestHandler BuildHandler(string city = "krakow")
    {
        File.WriteAllText(_tempFile, FixtureJson);
        var store = new MockDataStore(_tempFile, NullLogger<MockDataStore>.Instance);
        return new PricingRequestHandler(store, city);
    }

    public void Dispose() => File.Delete(_tempFile);

    private static DataQuery Query(string type, string city, string payloadJson)
        => new(type, city, JsonDocument.Parse(payloadJson).RootElement);

    // ── sukces ───────────────────────────────────────────────────────────────

    [Fact]
    public void Handle_KnownAttraction_ReturnsOkWithPricing()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":"wawel-castle"}"""));

        result.Should().BeOfType<QueryResponse>()
            .Which.Status.Should().Be("ok");
    }

    [Fact]
    public void Handle_KnownAttraction_DataContainsCurrencyAndPrices()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":"wawel-castle"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        var data     = JsonSerializer.SerializeToDocument(response.Data);
        var root     = data.RootElement;

        root.GetProperty("currency").GetString().Should().Be("PLN");
        root.GetProperty("prices").GetArrayLength().Should().Be(2);
        root.GetProperty("attractionId").GetString().Should().Be("wawel-castle");
    }

    [Fact]
    public void Handle_KnownAttraction_MetaHasMockSourceAndCorrectProvider()
    {
        var result = BuildHandler("krakow").Handle(
            Query("pricing", "krakow", """{"attractionId":"wawel-castle"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        response.Meta.Source.Should().Be("mock");
        response.Meta.Provider.Should().Be("pricing.krakow");
    }

    [Fact]
    public void Handle_AttractionIdWithWhitespace_Trims()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":"  wawel-castle  "}"""));

        result.Should().BeOfType<QueryResponse>()
            .Which.Status.Should().Be("ok");
    }

    // ── VALIDATION_ERROR ─────────────────────────────────────────────────────

    [Fact]
    public void Handle_MissingAttractionId_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{}"""));

        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_EmptyAttractionId_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":""}"""));

        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_NullPayload_ReturnsValidationError()
    {
        var query  = new DataQuery("pricing", "krakow", null);
        var result = BuildHandler().Handle(query);

        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_BrokenPayloads_NeverThrow()
    {
        // Weryfikuje kontrakt: każdy niepoprawny payload → VALIDATION_ERROR, nigdy wyjątek.
        // RpcConsumerBase polega na tym, że handler nigdy nie rzuca.
        var handler    = BuildHandler();
        var badQueries = new[]
        {
            new DataQuery("pricing", "krakow", null),
            Query("pricing", "krakow", "{}"),
            Query("pricing", "krakow", """{"attractionId":""}"""),
            Query("pricing", "krakow", """{"attractionId":"  "}"""),
        };

        foreach (var q in badQueries)
        {
            Action act = () => handler.Handle(q);
            act.Should().NotThrow(because: $"niepoprawny payload '{q.Payload}' musi dawać VALIDATION_ERROR, nie wyjątek");
        }
    }

    // ── ATTRACTION_NOT_FOUND ─────────────────────────────────────────────────

    [Fact]
    public void Handle_UnknownAttractionId_ReturnsAttractionNotFound()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":"nie-istnieje"}"""));

        AssertError(result, "ATTRACTION_NOT_FOUND");
    }

    [Fact]
    public void Handle_UnknownAttractionId_ErrorMessageContainsId()
    {
        var result = BuildHandler().Handle(
            Query("pricing", "krakow", """{"attractionId":"nie-istnieje"}"""));

        result.Should().BeOfType<ErrorResponse>()
            .Which.Error.Message.Should().Contain("nie-istnieje");
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static void AssertError(object result, string expectedCode)
        => result.Should().BeOfType<ErrorResponse>()
            .Which.Error.Code.Should().Be(expectedCode);
}
