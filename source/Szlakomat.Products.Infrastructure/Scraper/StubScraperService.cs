// TODO: zastąpić RabbitMqScraperService gdy infrastruktura kolejki gotowa
using Szlakomat.Products.Domain.Common;
using Szlakomat.Products.Domain.Scraper;

namespace Szlakomat.Products.Infrastructure.Scraper;

internal sealed class StubScraperService : IScraperService
{
    public Task<Result<string, IReadOnlyList<PlaceData>>> FetchAsync(
        ScraperRequest request,
        CancellationToken ct = default)
    {
        IReadOnlyList<PlaceData> places = new[]
        {
            new PlaceData
            {
                PlaceId = "stub-hotel-001",
                Name = "Hotel Stary Kraków",
                Address = "ul. Szczepańska 5, 31-011 Kraków",
                Phone = "+48 12 384 08 08",
                Website = "https://example-hotel-krakow.pl",
                Lat = 50.0619,
                Lng = 19.9370,
                OpeningHours = ["Recepcja czynna całą dobę"],
                Rating = 4.6,
                RatingCount = 1240,
                PriceLevel = 3,
                Photos = [new PlacePhoto("https://placeholder.com/hotel1.jpg", 800, 600)],
                Reviews = [new PlaceReview("Jan Kowalski", 5.0, "Świetna lokalizacja, polecam!", "2024-11-10")]
            },
            new PlaceData
            {
                PlaceId = "stub-attraction-001",
                Name = "Zamek Królewski na Wawelu",
                Address = "Wawel 5, 31-001 Kraków",
                Phone = "+48 12 422 51 55",
                Website = "https://example-wawel.pl",
                Lat = 50.0540,
                Lng = 19.9355,
                OpeningHours = ["Wt-Pt: 9:30-17:00", "Sob-Nd: 10:00-17:00", "Pn: nieczynne"],
                Rating = 4.8,
                RatingCount = 8750,
                PriceLevel = 2,
                Photos = [new PlacePhoto("https://placeholder.com/wawel1.jpg", 1200, 800)],
                Reviews = [new PlaceReview("Anna Nowak", 5.0, "Obowiązkowy punkt wizyty w Krakowie!", "2025-03-22")]
            },
            new PlaceData
            {
                PlaceId = "stub-restaurant-001",
                Name = "Restauracja Wierzynek",
                Address = "Rynek Główny 15, 31-008 Kraków",
                Phone = "+48 12 424 96 00",
                Website = "https://example-wierzynek.pl",
                Lat = 50.0617,
                Lng = 19.9382,
                OpeningHours = ["Pon-Nd: 12:00-23:00"],
                Rating = 4.4,
                RatingCount = 2315,
                PriceLevel = 4,
                Photos = [new PlacePhoto("https://placeholder.com/wierzynek1.jpg", 1024, 768)],
                Reviews = [new PlaceReview("Marek Wiśniewski", 4.0, "Tradycyjna kuchnia polska, klimatyczne wnętrze.", "2025-01-05")]
            }
        };

        return Task.FromResult(Result<string, IReadOnlyList<PlaceData>>.SuccessOf(places));
    }
}
