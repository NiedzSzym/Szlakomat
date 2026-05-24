namespace Szlakomat.Products.Domain.Scraper;

public record PlacePhoto(string Url, int? Width, int? Height);

public record PlaceReview(string Author, double Rating, string? Text, string? Time);

public record PlaceData
{
    public required string PlaceId { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public double? Lat { get; init; }
    public double? Lng { get; init; }
    public IReadOnlyList<string>? OpeningHours { get; init; }
    public double? Rating { get; init; }
    public int? RatingCount { get; init; }
    public int? PriceLevel { get; init; }
    public IReadOnlyList<PlacePhoto> Photos { get; init; } = [];
    public IReadOnlyList<PlaceReview> Reviews { get; init; } = [];
}
