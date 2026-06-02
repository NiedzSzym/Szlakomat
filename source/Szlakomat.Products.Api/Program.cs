using Szlakomat.Products.Infrastructure;
using Szlakomat.Products.Infrastructure.DataGateway;
using Szlakomat.Products.Infrastructure.Scraper;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProductModule();
builder.Services.AddScraperService();
builder.Services.AddDataGateway();

var app = builder.Build();
app.MapControllers();
app.Run();
