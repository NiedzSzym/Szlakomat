// TODO: zastąpić RabbitMqScraperService gdy infrastruktura kolejki gotowa
using System.Text.Json;
using Szlakomat.Products.Domain.Common;
using Szlakomat.Products.Domain.Scraper;

namespace Szlakomat.Products.Infrastructure.Scraper;

internal sealed class StubScraperService : IScraperService
{
    private const string StubJson = """
        {
          "provider": "stub",
          "dataType": "hotel",
          "total": 3,
          "error": null,
          "results": [
            {
              "placeId": "stub-hotel-001",
              "name": "Hotel Stary Kraków",
              "address": "ul. Szczepańska 5, 31-011 Kraków",
              "phone": "+48 12 384 08 08",
              "website": "https://example-hotel-krakow.pl",
              "lat": 50.0619,
              "lng": 19.9370,
              "openingHours": ["Recepcja czynna całą dobę"],
              "rating": 4.6,
              "ratingCount": 1240,
              "priceLevel": 3,
              "photos": [{ "url": "https://placeholder.com/hotel1.jpg", "width": 800, "height": 600 }],
              "reviews": [{ "author": "Jan Kowalski", "rating": 5.0, "text": "Świetna lokalizacja, polecam!", "time": "2024-11-10" }]
            },
            {
              "placeId": "stub-hotel-002",
              "name": "Hostel Kazimierz",
              "address": "ul. Miodowa 2, 31-055 Kraków",
              "phone": "+48 12 430 25 10",
              "website": "https://example-hostel-kazimierz.pl",
              "lat": 50.0510,
              "lng": 19.9450,
              "openingHours": ["Recepcja: 8:00-22:00"],
              "rating": 4.1,
              "ratingCount": 380,
              "priceLevel": 1,
              "photos": [{ "url": "https://placeholder.com/hostel2.jpg", "width": 800, "height": 600 }],
              "reviews": [{ "author": "Anna Nowak", "rating": 4.0, "text": "Dobra cena, spokojna okolica.", "time": "2024-10-05" }]
            },
            {
              "placeId": "stub-hotel-003",
              "name": "Apartamenty Wawel",
              "address": "ul. Grodzka 15, 31-006 Kraków",
              "phone": "+48 12 421 77 00",
              "website": "https://example-apartamenty-wawel.pl",
              "lat": 50.0574,
              "lng": 19.9368,
              "openingHours": null,
              "rating": 4.8,
              "ratingCount": 214,
              "priceLevel": 4,
              "photos": [{ "url": "https://placeholder.com/apt3.jpg", "width": 1200, "height": 800 }],
              "reviews": [{ "author": "Piotr Wiśniewski", "rating": 5.0, "text": "Widok na Wawel — bezcenny.", "time": "2024-09-18" }]
            }
          ]
        }
        """;

    public Task<Result<string, JsonDocument>> FetchAsync(
        ScraperRequest request,
        CancellationToken ct = default)
    {
        var doc = JsonDocument.Parse(StubJson);
        return Task.FromResult(Result<string, JsonDocument>.SuccessOf(doc));
    }
}
