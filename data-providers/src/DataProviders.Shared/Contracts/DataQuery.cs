using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataProviders.Shared.Contracts;

/// <summary>Wiadomość przychodząca od bramy danych.</summary>
public record DataQuery(
    [property: JsonPropertyName("type")]    string       Type,
    [property: JsonPropertyName("city")]    string       City,
    [property: JsonPropertyName("payload")] JsonElement? Payload
);
