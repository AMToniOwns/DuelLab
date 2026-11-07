using DuelLab.Api.Middleware;
using DuelLab.Application;
using DuelLab.Application.Common;
using DuelLab.Infrastructure;
using DuelLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DuelLabDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCaching();

app.MapOpenApi();
app.MapGet("/health", async (DuelLabDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapControllers();

// Unknown API routes are a 404 problem, never the web app's HTML.
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", extensions: new Dictionary<string, object?> { ["code"] = "not_found" }));

// In production the built Vue app is copied into wwwroot and served from the same origin; any other
// path falls back to index.html so client-side routes (e.g. /decks/123) work on reload.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the app.</summary>
public partial class Program;
