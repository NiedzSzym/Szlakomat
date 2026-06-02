using System.Text.Json;

namespace Szlakomat.Products.Api.Contracts.DataGateway;

public record QueryRequest(string Type, string City, JsonElement Payload);
