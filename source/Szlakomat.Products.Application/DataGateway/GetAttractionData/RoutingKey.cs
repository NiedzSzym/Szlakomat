namespace Szlakomat.Products.Application.DataGateway.GetAttractionData;

internal static class RoutingKey
{
    public static string From(string type)
        => type.Trim().ToLowerInvariant();
}
