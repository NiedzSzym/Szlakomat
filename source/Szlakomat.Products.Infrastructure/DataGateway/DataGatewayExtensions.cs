using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Infrastructure.DataGateway;

public static class DataGatewayExtensions
{
    public static IServiceCollection AddDataGateway(this IServiceCollection services)
    {
        services.AddScoped<IDataGateway, RabbitMqDataGateway>();
        // TODO: w produkcji zastąpić RabbitMqDataGateway realną implementacją z kolejką
        return services;
    }
}
