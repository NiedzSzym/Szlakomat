namespace Szlakomat.Products.Application.DataGateway;

public class DataGatewayOptions
{
    public string RabbitMqHost { get; set; } = "localhost";
    public string Exchange { get; set; } = "szlakomat.data";
    public int TimeoutSeconds { get; set; } = 10;
    public List<string> KnownProviders { get; set; } = [];
}
