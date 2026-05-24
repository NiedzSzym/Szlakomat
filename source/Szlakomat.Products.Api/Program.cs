using Szlakomat.Products.Infrastructure;
using Szlakomat.Products.Infrastructure.Scraper;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProductModule();
builder.Services.AddScraperService();

var app = builder.Build();
app.MapControllers();
app.Run();
