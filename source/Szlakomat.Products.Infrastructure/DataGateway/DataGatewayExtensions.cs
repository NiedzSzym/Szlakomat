using Microsoft.Extensions.DependencyInjection;
using Szlakomat.Products.Application.DataGateway;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Infrastructure.DataGateway;

public static class DataGatewayExtensions
{
    public static IServiceCollection AddDataGateway(this IServiceCollection services,
        Action<DataGatewayOptions>? configure = null)
    {
        var options = new DataGatewayOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<IDataGateway, RabbitMqDataGateway>();
        return services;
    }
}
