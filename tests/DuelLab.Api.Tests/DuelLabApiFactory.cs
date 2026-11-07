using DuelLab.Application.Cards;
using DuelLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DuelLab.Api.Tests;

/// <summary>
/// Hosts the real API against a real PostgreSQL database created for this test class and dropped
/// afterwards. Cards come from a local fixture file, so tests never touch the network.
/// </summary>
/// <remarks>
/// Set <c>DUELLAB_TEST_POSTGRES</c> to a connection string for a user that may create databases.
/// The default matches the CI service container and the local setup in the README.
/// </remarks>
public class DuelLabApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"duellab_test_{Guid.NewGuid():N}";
    private readonly string _imageCache = Path.Combine(Path.GetTempPath(), $"duellab-images-{Guid.NewGuid():N}");

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("DUELLAB_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=duellab;Password=duellab;Database=postgres";

    private string ConnectionString =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _databaseName }.ConnectionString;

    /// <summary>The card file the File provider reads; subclasses may point it elsewhere.</summary>
    protected virtual string CardFilePath => FixturePath;

    public static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "ygoprodeck-cards.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DuelLab", ConnectionString);
        builder.UseSetting("CardCatalog:Provider", "File");
        builder.UseSetting("CardCatalog:FilePath", CardFilePath);
        builder.UseSetting("CardCatalog:ImportOnStartup", "false");
        builder.UseSetting("CardCatalog:ImageCachePath", _imageCache);
    }

    public virtual async Task InitializeAsync()
    {
        // Starting the server applies migrations; then import the fixture cards once.
        _ = Server;
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CardCatalogService>().SyncAsync(force: true, CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await CleanupAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(_imageCache))
        {
            Directory.Delete(_imageCache, recursive: true);
        }
    }

    /// <summary>Extra cleanup for subclasses, after the server and database are gone.</summary>
    protected virtual Task CleanupAsync() => Task.CompletedTask;

    /// <summary>Runs code against the database directly (for assertions on stored data).</summary>
    public async Task WithDbAsync(Func<DuelLabDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<DuelLabDbContext>());
    }
}
