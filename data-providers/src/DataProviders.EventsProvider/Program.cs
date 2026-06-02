using DataProviders.EventsProvider;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<EventsConsumerService>();

var host = builder.Build();
host.Run();
