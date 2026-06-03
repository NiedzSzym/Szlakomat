using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DataProviders.Shared.MockData;

/// <summary>
/// Wczytuje attractions.json RAZ przy starcie i udostępnia dane atrakcji per miasto.
/// Bezpieczny dla wielu wątków — po inicjalizacji tylko do odczytu.
/// </summary>
public sealed class MockDataStore
{
    // city -> attractionId -> węzeł atrakcji
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> _cities;
    private readonly ILogger<MockDataStore> _logger;

    public MockDataStore(string filePath, ILogger<MockDataStore> logger)
    {
        _logger = logger;
        _cities = Load(filePath);
    }

    /// <summary>
    /// Zwraca węzeł atrakcji (name, pricing, events) albo null gdy nie znaleziono.
    /// Dopasowanie attractionId jest case-sensitive, ale wykonuje trim.
    /// </summary>
    public JsonElement? TryGetAttraction(string city, string attractionId)
    {
        var normalizedCity = city.Trim().ToLowerInvariant();
        var normalizedId   = attractionId.Trim();

        if (_cities.TryGetValue(normalizedCity, out var attractions)
            && attractions.TryGetValue(normalizedId, out var node))
            return node;

        return null;
    }

    // ── ładowanie ────────────────────────────────────────────────────────────

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError(
                "Plik mock-data nie istnieje: '{Path}'. " +
                "Ustaw zmienną MOCK_DATA_PATH lub sprawdź montowanie wolumenu.",
                filePath);
            return new Dictionary<string, IReadOnlyDictionary<string, JsonElement>>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var root = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json)
                       ?? [];

            var result = root.ToDictionary(
                cityKv => cityKv.Key.Trim().ToLowerInvariant(),
                cityKv => (IReadOnlyDictionary<string, JsonElement>)cityKv.Value
                    .ToDictionary(aKv => aKv.Key.Trim(), aKv => aKv.Value),
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Mock-data wczytane z '{Path}': {CityCount} miast, {AttractionCount} atrakcji łącznie.",
                filePath,
                result.Count,
                result.Values.Sum(c => c.Count));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd wczytywania pliku mock-data: '{Path}'.", filePath);
            return new Dictionary<string, IReadOnlyDictionary<string, JsonElement>>();
        }
    }
}
