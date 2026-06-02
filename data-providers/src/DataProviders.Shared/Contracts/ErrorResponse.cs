using System.Text.Json.Serialization;

namespace DataProviders.Shared.Contracts;

/// <summary>Odpowiedź błędu zgodna z kontraktem bramy danych.</summary>
public record ErrorResponse(
    [property: JsonPropertyName("status")] string    Status,
    [property: JsonPropertyName("error")]  ErrorInfo Error
);

public record ErrorInfo(
    [property: JsonPropertyName("code")]    string Code,
    [property: JsonPropertyName("message")] string Message
);
