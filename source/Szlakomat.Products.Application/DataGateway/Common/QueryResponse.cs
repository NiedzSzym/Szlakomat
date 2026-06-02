namespace Szlakomat.Products.Application.DataGateway.Common;

public record QueryResponse(string Status, string Type, string City, object? Data, object? Meta);
