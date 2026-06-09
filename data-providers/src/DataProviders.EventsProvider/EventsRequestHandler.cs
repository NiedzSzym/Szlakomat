using System.Text.Json;
using DataProviders.Shared.Contracts;
using DataProviders.Shared.MockData;

namespace DataProviders.EventsProvider;

internal sealed class EventsRequestHandler
{
    private readonly MockDataStore _store;

    public EventsRequestHandler(MockDataStore store) => _store = store;

    public object Handle(DataQuery query)
    {
        var attractionId = ExtractAttractionId(query.Payload);
        if (string.IsNullOrWhiteSpace(attractionId))
            return Error("VALIDATION_ERROR", "Pole 'attractionId' jest wymagane");

        DateOnly? from = null, to = null;

        if (query.Payload.HasValue && query.Payload.Value.ValueKind == JsonValueKind.Object)
        {
            if (query.Payload.Value.TryGetProperty("from", out var fromEl))
            {
                var fromStr = fromEl.GetString();
                if (fromStr is not null)
                {
                    if (!DateOnly.TryParseExact(fromStr, "yyyy-MM-dd", out var parsedFrom))
                        return Error("VALIDATION_ERROR", "Niepoprawny format daty, oczekiwano YYYY-MM-DD");
                    from = parsedFrom;
                }
            }
            if (query.Payload.Value.TryGetProperty("to", out var toEl))
            {
                var toStr = toEl.GetString();
                if (toStr is not null)
                {
                    if (!DateOnly.TryParseExact(toStr, "yyyy-MM-dd", out var parsedTo))
                        return Error("VALIDATION_ERROR", "Niepoprawny format daty, oczekiwano YYYY-MM-DD");
                    to = parsedTo;
                }
            }
        }

        var node = _store.TryGetAttraction(query.City, attractionId);
        if (node is null)
            return Error("ATTRACTION_NOT_FOUND",
                $"Nie znaleziono atrakcji '{attractionId}' w '{query.City}'");

        var data = BuildEventsData(attractionId, node.Value, from, to);
        return new QueryResponse(
            Status: "ok",
            Type:   query.Type,
            City:   query.City,
            Data:   data,
            Meta:   new ResponseMeta($"events.{query.City}", "mock"));
    }

    private static string? ExtractAttractionId(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        return payload.Value.TryGetProperty("attractionId", out var el)
            ? el.GetString()?.Trim()
            : null;
    }

    private static object BuildEventsData(
        string attractionId, JsonElement node, DateOnly? from, DateOnly? to)
    {
        var events = new List<JsonElement>();

        if (node.TryGetProperty("events", out var eventsEl)
            && eventsEl.TryGetProperty("upcoming", out var upcoming)
            && upcoming.ValueKind == JsonValueKind.Array)
        {
            foreach (var ev in upcoming.EnumerateArray())
            {
                if ((from.HasValue || to.HasValue) && !EventOverlaps(ev, from, to))
                    continue;
                events.Add(ev);
            }
        }

        return new { attractionId, events };
    }

    private static bool EventOverlaps(JsonElement ev, DateOnly? from, DateOnly? to)
    {
        DateOnly? evStart = null, evEnd = null;

        if (ev.TryGetProperty("date", out var dateEl))
        {
            var s = dateEl.GetString();
            if (s is not null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d))
                evStart = evEnd = d;
        }
        else
        {
            if (ev.TryGetProperty("from", out var fromEl))
            {
                var s = fromEl.GetString();
                if (s is not null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d))
                    evStart = d;
            }
            if (ev.TryGetProperty("to", out var toEl))
            {
                var s = toEl.GetString();
                if (s is not null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d))
                    evEnd = d;
            }
        }

        if (evStart is null || evEnd is null)
            return true; // nieznany format — dołącz

        // Przecięcie przedziałów: evStart <= to AND evEnd >= from
        if (to.HasValue   && evStart > to)   return false;
        if (from.HasValue && evEnd   < from) return false;
        return true;
    }

    private static ErrorResponse Error(string code, string message)
        => new("error", new ErrorInfo(code, message));
}
