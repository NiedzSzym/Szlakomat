using System.Text.Json.Serialization;

namespace DataProviders.Shared.Contracts;

/// <summary>Odpowiedź odsyłana z powrotem do bramy (sukces).</summary>
public record QueryResponse(
    [property: JsonPropertyName("status")] string       Status,
    [property: JsonPropertyName("type")]   string       Type,
    [property: JsonPropertyName("city")]   string       City,
    [property: JsonPropertyName("data")]   object?      Data,
    [property: JsonPropertyName("meta")]   ResponseMeta Meta
);

public record ResponseMeta(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("source")]   string Source
);
