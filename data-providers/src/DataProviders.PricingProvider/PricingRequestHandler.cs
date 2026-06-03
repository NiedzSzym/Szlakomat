using System.Text.Json;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;

namespace DataProviders.PricingProvider;

/// <summary>
/// Czysta logika obsługi zapytania cennikowego — bez zależności I/O, w pełni testowalna.
/// </summary>
internal sealed class PricingRequestHandler
{
    private readonly MockDataStore _store;
    private readonly string        _city;

    public PricingRequestHandler(MockDataStore store, string city)
    {
        _store = store;
        _city  = city;
    }

    /// <summary>
    /// Zwraca <see cref="QueryResponse"/> (sukces) albo <see cref="ErrorResponse"/> (błąd).
    /// </summary>
    public object Handle(DataQuery query)
    {
        // 1. Wyciągnij attractionId z payloadu
        var attractionId = ExtractAttractionId(query.Payload);
        if (string.IsNullOrWhiteSpace(attractionId))
            return Error("VALIDATION_ERROR", "Pole 'attractionId' jest wymagane");

        // 2. Wyszukaj atrakcję w store
        var node = _store.TryGetAttraction(_city, attractionId);
        if (node is null)
            return Error("ATTRACTION_NOT_FOUND",
                $"Nie znaleziono atrakcji '{attractionId}' w '{_city}'");

        // 3. Zbuduj odpowiedź z aspektu pricing
        var data = BuildPricingData(attractionId, node.Value);
        return new QueryResponse(
            Status: "ok",
            Type:   query.Type,
            City:   query.City,
            Data:   data,
            Meta:   new ResponseMeta($"pricing.{_city}", "mock"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string? ExtractAttractionId(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;

        return payload.Value.TryGetProperty("attractionId", out var el)
            ? el.GetString()?.Trim()
            : null;
    }

    private static object BuildPricingData(string attractionId, JsonElement node)
    {
        string? currency = null;
        object? prices   = null;

        if (node.TryGetProperty("pricing", out var pricingEl))
        {
            if (pricingEl.TryGetProperty("currency", out var cEl))
                currency = cEl.GetString();
            if (pricingEl.TryGetProperty("prices", out var prEl))
                prices = prEl;
        }

        return new { attractionId, currency, prices };
    }

    private static ErrorResponse Error(string code, string message)
        => new("error", new ErrorInfo(code, message));
}
