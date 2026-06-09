using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Szlakomat.Products.Infrastructure;
using Szlakomat.Products.Infrastructure.DataGateway;
using Szlakomat.Products.Infrastructure.Scraper;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        // Niepoprawny/nieparsowalny JSON, brak ciała → { status:"error", error:{ code:"VALIDATION_ERROR" } }
        opts.InvalidModelStateResponseFactory = ctx =>
        {
            var messages = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage)
                    ? "Błąd deserializacji ciała żądania"
                    : e.ErrorMessage)
                .ToList();
            var message = messages.Count > 0
                ? string.Join("; ", messages)
                : "Niepoprawne dane wejściowe";
            return new ObjectResult(new
            {
                status = "error",
                error  = new { code = "VALIDATION_ERROR", message }
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        };
    });

builder.Services.AddProductModule();
builder.Services.AddScraperService();
builder.Services.AddDataGateway(opts =>
    builder.Configuration.GetSection("DataGateway").Bind(opts));

var app = builder.Build();

// ── 1. Globalny handler wyjątków — bez wycieku stack trace ──────────────────
app.UseExceptionHandler(exApp =>
{
    exApp.Run(async ctx =>
    {
        ctx.Response.StatusCode  = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = "error",
            error  = new { code = "INTERNAL_ERROR", message = "Błąd wewnętrzny" }
        });
    });
});

// ── 2. 415 Unsupported Media Type → 400 VALIDATION_ERROR ────────────────────
// [ApiController] zwraca 415 zanim dotrze do handlera; buforujemy odpowiedź
// i podmieniamy kształt na spójny z resztą bramy.
app.Use(async (ctx, next) =>
{
    var originalBody = ctx.Response.Body;
    var buffer       = new MemoryStream();
    ctx.Response.Body = buffer;

    Exception? fault = null;
    try   { await next(); }
    catch (Exception ex) { fault = ex; }

    ctx.Response.Body = originalBody;  // przywróć przed jakimkolwiek zapisem

    if (fault is not null)
        ExceptionDispatchInfo.Capture(fault).Throw();

    if (ctx.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            status = "error",
            error  = new { code = "VALIDATION_ERROR", message = "Wymagany Content-Type: application/json" }
        });
        ctx.Response.StatusCode    = StatusCodes.Status400BadRequest;
        ctx.Response.ContentType   = "application/json";
        ctx.Response.ContentLength = json.Length;
        await originalBody.WriteAsync(json);
        return;
    }

    buffer.Position = 0;
    await buffer.CopyToAsync(originalBody);
});

app.MapControllers();
app.Run();

// Wymagane do WebApplicationFactory<Program> w testach HTTP
public partial class Program { }
