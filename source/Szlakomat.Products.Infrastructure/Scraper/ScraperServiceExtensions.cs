using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Domain.Scraper;

namespace Szlakomat.Products.Infrastructure.Scraper;

public static class ScraperServiceExtensions
{
    public static IServiceCollection AddScraperService(this IServiceCollection services)
    {
        services.AddScoped<IScraperService, StubScraperService>();
        // TODO: w produkcji: services.AddScoped<IScraperService, RabbitMqScraperService>();
        return services;
    }
}
