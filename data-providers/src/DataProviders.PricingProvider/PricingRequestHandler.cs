using System.Text.Json;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;

namespace DataProviders.PricingProvider;

internal sealed class PricingRequestHandler
{
    private readonly MockDataStore _store;

    public PricingRequestHandler(MockDataStore store) => _store = store;

    public object Handle(DataQuery query)
    {
        var attractionId = ExtractAttractionId(query.Payload);
        if (string.IsNullOrWhiteSpace(attractionId))
            return Error("VALIDATION_ERROR", "Pole 'attractionId' jest wymagane");

        var node = _store.TryGetAttraction(query.City, attractionId);
        if (node is null)
            return Error("ATTRACTION_NOT_FOUND",
                $"Nie znaleziono atrakcji '{attractionId}' w '{query.City}'");

        var data = BuildPricingData(attractionId, node.Value);
        return new QueryResponse(
            Status: "ok",
            Type:   query.Type,
            City:   query.City,
            Data:   data,
            Meta:   new ResponseMeta($"pricing.{query.City}", "mock"));
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
