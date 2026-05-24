using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Domain.Scraper;

public interface IScraperService
{
    Task<Result<string, IReadOnlyList<PlaceData>>> FetchAsync(
        ScraperRequest request,
        CancellationToken ct = default);
}
