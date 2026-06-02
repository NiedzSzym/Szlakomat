using DataProviders.PricingProvider;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<PricingConsumerService>();

var host = builder.Build();
host.Run();
