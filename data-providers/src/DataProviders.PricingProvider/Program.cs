using DataProviders.PricingProvider;
using DataProviders.Shared.MockData;

var builder = Host.CreateApplicationBuilder(args);

var mockDataPath = builder.Configuration["MOCK_DATA_PATH"] ?? "mock-data/attractions.json";
builder.Services.AddSingleton(sp =>
    new MockDataStore(mockDataPath, sp.GetRequiredService<ILogger<MockDataStore>>()));

builder.Services.AddHostedService<PricingConsumerService>();

var host = builder.Build();
host.Run();
