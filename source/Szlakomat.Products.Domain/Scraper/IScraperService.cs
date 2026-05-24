using System.Text.Json;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Domain.Scraper;

/// <summary>
/// Pobiera dane turystyczne od zewnętrznego providera.
/// Zwrócony <see cref="JsonDocument"/> jest surową odpowiedzią z mikroserwisu Python —
/// jego struktura zależy od <see cref="ScraperRequest.DataType"/> i
/// <see cref="ScraperRequest.Provider"/>. Konsument jest odpowiedzialny za
/// interpretację i deserializację payload oraz za wywołanie
/// <see cref="JsonDocument.Dispose"/> po zakończeniu pracy z dokumentem.
/// </summary>
// Przykład użycia:
// var result = await scraper.FetchAsync(request, ct);
// if (!result.IsSuccess())
//     return Result<string, MyModel>.FailureOf(result.GetFailure());
//
// using var doc = result.GetSuccess();
// var hotels = doc.RootElement
//     .GetProperty("results")
//     .Deserialize<List<MyHotelModel>>();
public interface IScraperService
{
    Task<Result<string, JsonDocument>> FetchAsync(
        ScraperRequest request,
        CancellationToken ct = default);
}
