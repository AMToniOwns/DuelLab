using DuelLab.Api;
using DuelLab.Api.Middleware;
using DuelLab.Application;
using DuelLab.Application.Common;
using DuelLab.Infrastructure;
using DuelLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// Desktop mode (the Windows download) serves files from the program's own folder, whatever folder it
// was started from.
var desktop = DesktopMode.IsEnabled;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = desktop ? AppContext.BaseDirectory : null,
});

string? desktopUrl = null;
if (desktop)
{
    desktopUrl = await DesktopMode.ConfigureAsync(builder, args);
    if (desktopUrl is null)
    {
        return; // Already running: its page was opened.
    }
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => JsonDefaults.Apply(o.JsonSerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddResponseCaching();

// Compress JSON and the web app's scripts and styles (measured: initial page 154 KB -> 65 KB). Safe because
// responses carry no secrets that a compression side channel could reveal.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    o.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "application/problem+json"]);
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DuelLabDbContext>().Database.MigrateAsync();
}

app.UseResponseCompression();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCaching();

app.MapOpenApi();
app.MapGet("/health", async (DuelLabDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy", app = "DuelLab" }) : Results.StatusCode(503));
app.MapControllers();

// Unknown API routes are a 404 problem, never the web app's HTML.
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", extensions: new Dictionary<string, object?> { ["code"] = "not_found" }));

// In production the built Vue app is copied into wwwroot and served from the same origin; any other
// path falls back to index.html so client-side routes (e.g. /decks/123) work on reload.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.StartAsync();
if (desktopUrl is not null)
{
    DesktopMode.OnStarted(desktopUrl, args);
}

await app.WaitForShutdownAsync();

/// <summary>Entry point; public so integration tests can host the app.</summary>
public partial class Program;
