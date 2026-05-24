namespace Szlakomat.Products.Domain.Scraper;

public record ScraperRequest
{
    public required TouristDataType DataType { get; init; }
    public required GeoLocation Location { get; init; }
    public string? Query { get; init; }
    public string Language { get; init; } = "pl";
    public int MaxResults { get; init; } = 10;
    public ProviderHint? Provider { get; init; } // null = router po stronie Python decyduje
}
