using System.Text.Json;
using DataProviders.EventsProvider;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataProviders.Tests.Events;

public class EventsRequestHandlerTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    // 3 events: single date July-15, range July-20..22, single date Aug-20
    private static readonly string FixtureJson = """
        {
          "krakow": {
            "wawel-castle": {
              "name": "Zamek Wawelski",
              "events": {
                "upcoming": [
                  { "title": "Noc Wawelska",   "date": "2026-07-15" },
                  { "title": "Targi Letnie",    "from": "2026-07-20", "to": "2026-07-22" },
                  { "title": "Festiwal Smoka",  "date": "2026-08-20" }
                ]
              }
            }
          }
        }
        """;

    private EventsRequestHandler BuildHandler(string city = "krakow")
    {
        File.WriteAllText(_tempFile, FixtureJson);
        var store = new MockDataStore(_tempFile, NullLogger<MockDataStore>.Instance);
        return new EventsRequestHandler(store, city);
    }

    public void Dispose() => File.Delete(_tempFile);

    private static DataQuery Query(string type, string city, string payloadJson)
        => new(type, city, JsonDocument.Parse(payloadJson).RootElement);

    // ── sukces ───────────────────────────────────────────────────────────────

    [Fact]
    public void Handle_KnownAttraction_ReturnsOkWithEvents()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow", """{"attractionId":"wawel-castle"}"""));

        result.Should().BeOfType<QueryResponse>()
            .Which.Status.Should().Be("ok");
    }

    [Fact]
    public void Handle_KnownAttraction_DataContainsAllThreeEvents()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow", """{"attractionId":"wawel-castle"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        var data     = JsonSerializer.SerializeToDocument(response.Data);
        data.RootElement.GetProperty("events").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void Handle_KnownAttraction_MetaHasMockSourceAndCorrectProvider()
    {
        var result = BuildHandler("krakow").Handle(
            Query("events", "krakow", """{"attractionId":"wawel-castle"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        response.Meta.Source.Should().Be("mock");
        response.Meta.Provider.Should().Be("events.krakow");
    }

    // ── filtr dat ────────────────────────────────────────────────────────────

    [Fact]
    public void Handle_WithFromToFilter_ReturnsOnlyOverlappingEvents()
    {
        // [07-15, 07-31] — Noc Wawelska (07-15) ✓, Targi (07-20..22) ✓, Festiwal (08-20) ✗
        var result = BuildHandler().Handle(
            Query("events", "krakow",
                """{"attractionId":"wawel-castle","from":"2026-07-15","to":"2026-07-31"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        var data     = JsonSerializer.SerializeToDocument(response.Data);
        data.RootElement.GetProperty("events").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Handle_WithFromOnlyFilter_ExcludesEarlierEvents()
    {
        // from=08-01 — Noc (07-15) ✗, Targi (07-20..22) ✗, Festiwal (08-20) ✓
        var result = BuildHandler().Handle(
            Query("events", "krakow",
                """{"attractionId":"wawel-castle","from":"2026-08-01"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        var data     = JsonSerializer.SerializeToDocument(response.Data);
        data.RootElement.GetProperty("events").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Handle_WithToOnlyFilter_ExcludesLaterEvents()
    {
        // to=07-19 — Noc (07-15) ✓, Targi (07-20..22) ✗ (start=07-20 > to=07-19), Festiwal (08-20) ✗
        var result = BuildHandler().Handle(
            Query("events", "krakow",
                """{"attractionId":"wawel-castle","to":"2026-07-19"}"""));

        var response = result.Should().BeOfType<QueryResponse>().Subject;
        var data     = JsonSerializer.SerializeToDocument(response.Data);
        data.RootElement.GetProperty("events").GetArrayLength().Should().Be(1);
    }

    // ── VALIDATION_ERROR ─────────────────────────────────────────────────────

    [Fact]
    public void Handle_MissingAttractionId_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow", """{}"""));

        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_NullPayload_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(new DataQuery("events", "krakow", null));

        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_InvalidFromDate_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow",
                """{"attractionId":"wawel-castle","from":"not-a-date"}"""));

        result.Should().BeOfType<ErrorResponse>()
            .Which.Error.Message.Should().Contain("YYYY-MM-DD");
        AssertError(result, "VALIDATION_ERROR");
    }

    [Fact]
    public void Handle_InvalidToDate_ReturnsValidationError()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow",
                """{"attractionId":"wawel-castle","from":"2026-07-01","to":"2026/07/31"}"""));

        AssertError(result, "VALIDATION_ERROR");
    }

    // ── ATTRACTION_NOT_FOUND ─────────────────────────────────────────────────

    [Fact]
    public void Handle_UnknownAttractionId_ReturnsAttractionNotFound()
    {
        var result = BuildHandler().Handle(
            Query("events", "krakow", """{"attractionId":"nie-istnieje"}"""));

        AssertError(result, "ATTRACTION_NOT_FOUND");
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static void AssertError(object result, string expectedCode)
        => result.Should().BeOfType<ErrorResponse>()
            .Which.Error.Code.Should().Be(expectedCode);
}
