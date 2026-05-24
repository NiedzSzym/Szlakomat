namespace Szlakomat.Products.Domain.Scraper;

public record GeoLocation(double Lat, double Lng, int RadiusMeters = 1000);
