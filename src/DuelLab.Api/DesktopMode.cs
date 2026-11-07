using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DuelLab.Api;

/// <summary>
/// Desktop mode: DuelLab running as a program on someone's own PC (the Windows download). It is on
/// when <c>appsettings.Desktop.json</c> sits next to the program; only the Windows package has that
/// file, so servers and Docker never enter this mode.
/// </summary>
/// <remarks>
/// In desktop mode DuelLab stores its SQLite database and card images in the user's local app-data
/// folder, listens on 127.0.0.1 only (no firewall prompt, not reachable from the network), opens the
/// browser when it is ready, and, if it is already running, just opens the browser again and exits.
/// </remarks>
internal static class DesktopMode
{
    public const string SettingsFile = "appsettings.Desktop.json";
    private const int DefaultPort = 5180;

    /// <summary>True when the desktop settings file is next to the program.</summary>
    public static bool IsEnabled => File.Exists(Path.Combine(AppContext.BaseDirectory, SettingsFile));

    /// <summary>The folder for the database and card images (override with DUELLAB_DATA_DIR).</summary>
    public static string DataDirectory =>
        Environment.GetEnvironmentVariable("DUELLAB_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuelLab");

    /// <summary>
    /// Applies desktop settings. Returns the URL to listen on, or null when another DuelLab is already
    /// running (its page has then been opened and this process should exit).
    /// </summary>
    public static async Task<string?> ConfigureAsync(WebApplicationBuilder builder, string[] args)
    {
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, SettingsFile), optional: false);

        var dataDirectory = DataDirectory;
        Directory.CreateDirectory(dataDirectory);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:DuelLab"] = $"Data Source={Path.Combine(dataDirectory, "duellab.db")}",
            ["CardCatalog:ImageCachePath"] = Path.Combine(dataDirectory, "card-images"),
        });

        // Environment variables and command-line options still win over the desktop defaults (CI uses
        // this to point the app at the offline card fixture).
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(args);

        var preferred = builder.Configuration.GetValue("Desktop:Port", DefaultPort);
        foreach (var port in Enumerable.Range(preferred, 10))
        {
            var url = $"http://127.0.0.1:{port}";
            if (IsFree(port))
            {
                builder.WebHost.UseUrls(url);
                return url;
            }

            if (await IsDuelLabAsync(url))
            {
                Console.WriteLine($"DuelLab is already running at {url}. Opening it in your browser.");
                OpenBrowser(url, args);
                return null;
            }
        }

        throw new InvalidOperationException($"Ports {preferred}–{preferred + 9} are all in use. Close another program, or set Desktop:Port in {SettingsFile}.");
    }

    /// <summary>Tells the user where DuelLab is and opens it in the browser.</summary>
    public static void OnStarted(string url, string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("  DuelLab is running.");
        Console.WriteLine($"  Open {url} in your browser if it does not open by itself.");
        Console.WriteLine($"  Your decks are stored in {DataDirectory}");
        Console.WriteLine("  Close this window (or press Ctrl+C) to stop DuelLab.");
        Console.WriteLine();
        OpenBrowser(url, args);
    }

    private static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<bool> IsDuelLabAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var body = await http.GetStringAsync($"{url}/health");
            return body.Contains("\"app\":\"DuelLab\"", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static void OpenBrowser(string url, string[] args)
    {
        if (args.Contains("--no-browser"))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // No browser could be started (e.g. a server without a desktop); the console shows the URL.
        }
    }
}
