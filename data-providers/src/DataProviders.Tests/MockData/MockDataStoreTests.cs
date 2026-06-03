using DataProviders.Shared.MockData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataProviders.Tests.MockData;

public class MockDataStoreTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    private MockDataStore BuildStore(string json)
    {
        File.WriteAllText(_tempFile, json);
        return new MockDataStore(_tempFile, NullLogger<MockDataStore>.Instance);
    }

    public void Dispose() => File.Delete(_tempFile);

    // ── TryGetAttraction — znana atrakcja ────────────────────────────────────

    [Fact]
    public void TryGetAttraction_KnownCityAndId_ReturnsNode()
    {
        var store = BuildStore("""
            {
              "krakow": {
                "wawel-castle": { "name": "Zamek Wawelski" }
              }
            }
            """);

        var node = store.TryGetAttraction("krakow", "wawel-castle");

        node.Should().NotBeNull();
        node!.Value.GetProperty("name").GetString().Should().Be("Zamek Wawelski");
    }

    [Fact]
    public void TryGetAttraction_CityNameCaseInsensitive_ReturnsNode()
    {
        var store = BuildStore("""
            { "krakow": { "wawel-castle": { "name": "Zamek" } } }
            """);

        store.TryGetAttraction("KRAKOW", "wawel-castle").Should().NotBeNull();
        store.TryGetAttraction("Krakow", "wawel-castle").Should().NotBeNull();
    }

    [Fact]
    public void TryGetAttraction_AttractionIdWithWhitespace_Trims()
    {
        var store = BuildStore("""
            { "krakow": { "wawel-castle": { "name": "Zamek" } } }
            """);

        store.TryGetAttraction("krakow", "  wawel-castle  ").Should().NotBeNull();
    }

    // ── TryGetAttraction — nieznane ──────────────────────────────────────────

    [Fact]
    public void TryGetAttraction_UnknownCity_ReturnsNull()
    {
        var store = BuildStore("""
            { "krakow": { "wawel-castle": {} } }
            """);

        store.TryGetAttraction("warszawa", "wawel-castle").Should().BeNull();
    }

    [Fact]
    public void TryGetAttraction_UnknownAttractionId_ReturnsNull()
    {
        var store = BuildStore("""
            { "krakow": { "wawel-castle": {} } }
            """);

        store.TryGetAttraction("krakow", "nie-istnieje").Should().BeNull();
    }

    // ── błąd pliku ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NonExistentFile_ReturnsEmptyStore()
    {
        var store = new MockDataStore("/non/existent/path.json", NullLogger<MockDataStore>.Instance);

        store.TryGetAttraction("krakow", "wawel-castle").Should().BeNull();
    }
}
