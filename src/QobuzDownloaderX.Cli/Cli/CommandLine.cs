using QobuzDownloaderX.Cli.Config;
using QobuzDownloaderX.Cli.Download;
using QobuzDownloaderX.Cli.Qobuz;

namespace QobuzDownloaderX.Cli.Cli;

/// <summary>
/// Entry point and command dispatcher for the cross-platform CLI. Replaces the
/// WinForms LoginForm + qbdlxForm. Subcommands: login, get, config.
/// </summary>
public static class CommandLine
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToArray();
            var opts = new ArgMap(rest);

            return command switch
            {
                "login" => await LoginAsync(opts),
                "get" or "download" or "dl" => await GetAsync(opts),
                "config" => Config(opts),
                "version" or "--version" or "-v" => Version(),
                _ => Unknown(command),
            };
        }
        catch (QobuzApiException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"  ({ex.InnerException.Message})");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LoginAsync(ArgMap opts)
    {
        var cfg = AppConfig.Load();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var api = new QobuzApiClient(http);

        Console.WriteLine("Fetching app credentials from the Qobuz web player...");
        await api.BootstrapAppCredentialsAsync();
        Console.WriteLine($"  app_id: {api.AppId}");

        string? token = opts.Get("token");
        if (!string.IsNullOrEmpty(token))
        {
            await api.LoginWithTokenAsync(token);
        }
        else
        {
            string? email = opts.Get("email") ?? Prompt("Email: ");
            string? password = opts.Get("password") ?? PromptHidden("Password: ");
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("Email and password (or --token) are required.");
                return 1;
            }
            await api.LoginWithEmailAsync(email, password);
        }

        cfg.AppId = api.AppId;
        cfg.AppSecret = api.AppSecret;
        cfg.UserAuthToken = api.UserAuthToken;
        cfg.UserId = api.UserId;
        cfg.DisplayName = api.DisplayName;
        cfg.Save();

        Console.WriteLine($"Logged in as {(string.IsNullOrEmpty(api.DisplayName) ? api.UserId : api.DisplayName)}.");
        Console.WriteLine($"Credentials saved to {AppConfig.ConfigPath}");
        return 0;
    }

    private static async Task<int> GetAsync(ArgMap opts)
    {
        var cfg = AppConfig.Load();
        if (!cfg.HasCredentials)
        {
            Console.Error.WriteLine("Not logged in. Run `qobuz-dl-x login` first.");
            return 1;
        }

        if (opts.Get("quality") is { } q) cfg.QualityFormatId = NormalizeQuality(q);
        if (opts.Get("out") is { } outDir) cfg.DownloadFolder = outDir;
        if (opts.Get("output") is { } outDir2) cfg.DownloadFolder = outDir2;

        var targets = opts.Positionals;
        if (targets.Count == 0)
        {
            Console.Error.WriteLine("Provide at least one Qobuz URL or id. Example:");
            Console.Error.WriteLine("  qobuz-dl-x get https://open.qobuz.com/album/xxxxxxxxxxxxx");
            return 1;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        var api = new QobuzApiClient(http);
        api.UseCredentials(cfg.AppId!, cfg.AppSecret ?? "", cfg.UserAuthToken!, cfg.UserId ?? "", cfg.DisplayName ?? "");

        // Refresh app secrets if none stored (older config) so signing works.
        if (string.IsNullOrEmpty(cfg.AppSecret))
        {
            await api.BootstrapAppCredentialsAsync();
            api.UseCredentials(api.AppId, api.AppSecret, cfg.UserAuthToken!, cfg.UserId ?? "", cfg.DisplayName ?? "");
        }

        Console.WriteLine($"Quality: {QualityName(cfg.QualityFormatId)}");
        Console.WriteLine($"Output:  {cfg.DownloadFolder}");

        var downloader = new Downloader(api, cfg, http);
        int failures = 0;
        foreach (var target in targets)
        {
            var entity = QobuzUrl.Parse(target);
            if (entity.Type == QobuzEntityType.Unknown)
            {
                Console.Error.WriteLine($"Skipping unrecognized reference: {target}");
                failures++;
                continue;
            }
            try
            {
                await downloader.DownloadEntityAsync(entity, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to download {target}: {ex.Message}");
                failures++;
            }
        }
        return failures == 0 ? 0 : 1;
    }

    private static int Config(ArgMap opts)
    {
        var cfg = AppConfig.Load();
        string action = opts.Positionals.FirstOrDefault()?.ToLowerInvariant() ?? "show";

        if (action == "show")
        {
            Console.WriteLine($"Config file:    {AppConfig.ConfigPath}");
            Console.WriteLine($"Logged in:      {cfg.HasCredentials} ({cfg.DisplayName})");
            Console.WriteLine($"Download folder:{cfg.DownloadFolder}");
            Console.WriteLine($"Quality:        {QualityName(cfg.QualityFormatId)} (format_id {cfg.QualityFormatId})");
            Console.WriteLine($"Album template: {cfg.AlbumTemplate}");
            Console.WriteLine($"Track template: {cfg.TrackTemplate}");
            Console.WriteLine($"Playlist tmpl:  {cfg.PlaylistTemplate}");
            return 0;
        }

        if (action == "set")
        {
            bool changed = false;
            if (opts.Get("quality") is { } q) { cfg.QualityFormatId = NormalizeQuality(q); changed = true; }
            if (opts.Get("out") is { } o) { cfg.DownloadFolder = o; changed = true; }
            if (opts.Get("album-template") is { } at) { cfg.AlbumTemplate = at; changed = true; }
            if (opts.Get("track-template") is { } tt) { cfg.TrackTemplate = tt; changed = true; }
            if (opts.Get("playlist-template") is { } pt) { cfg.PlaylistTemplate = pt; changed = true; }
            if (changed) { cfg.Save(); Console.WriteLine("Config updated."); }
            else Console.WriteLine("Nothing to set. See `qobuz-dl-x help`.");
            return 0;
        }

        Console.Error.WriteLine("Usage: qobuz-dl-x config [show|set ...]");
        return 1;
    }

    private static int Version()
    {
        Console.WriteLine("QobuzDownloaderX CLI (cross-platform) — 2.0.0");
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    // ---- helpers ----

    private static string NormalizeQuality(string q) => q.Trim().ToLowerInvariant() switch
    {
        "mp3" or "320" or "5" => "5",
        "flac" or "cd" or "lossless" or "6" => "6",
        "hires" or "96" or "7" => "7",
        "max" or "192" or "27" => "27",
        _ => q,
    };

    private static string QualityName(string formatId) => formatId switch
    {
        "5" => "MP3 320kbps",
        "6" => "FLAC 16-bit/44.1kHz",
        "7" => "FLAC 24-bit up to 96kHz",
        "27" => "FLAC 24-bit up to 192kHz",
        _ => $"format_id {formatId}",
    };

    private static bool IsHelp(string a) =>
        a is "help" or "-h" or "--help";

    private static string? Prompt(string label)
    {
        Console.Write(label);
        return Console.ReadLine();
    }

    private static string PromptHidden(string label)
    {
        Console.Write(label);
        var sb = new System.Text.StringBuilder();
        try
        {
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) sb.Length--;
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // No interactive console (e.g. piped input) — fall back to ReadLine.
            return Console.ReadLine() ?? "";
        }
        Console.WriteLine();
        return sb.ToString();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"QobuzDownloaderX — cross-platform CLI (macOS / Linux / Windows)

USAGE
  qobuz-dl-x <command> [options]

COMMANDS
  login                 Authenticate and save credentials
      --email <e>       Account email
      --password <p>    Account password (prompted if omitted)
      --token <t>       Log in with an existing user_auth_token instead

  get <url|id> [...]    Download album / track / playlist / artist / label
      --quality <q>     mp3 | flac | hires | max  (or 5 | 6 | 7 | 27)
      --out <dir>       Output folder for this run

  config show           Show current configuration
  config set [...]      Persist settings:
      --quality <q>     Default quality
      --out <dir>       Default download folder
      --album-template <t>
      --track-template <t>
      --playlist-template <t>

  version               Print version
  help                  Show this help

EXAMPLES
  qobuz-dl-x login --email me@example.com
  qobuz-dl-x login --token 6Hx...your_token...
  qobuz-dl-x get https://open.qobuz.com/album/xxxxxxxxxxxxx --quality max
  qobuz-dl-x get album/xxxxxxxxxxxxx track/12345678 --out ~/Music/Qobuz

Config is stored at:
  " + AppConfig.ConfigPath + @"

NOTE
  This tool downloads only what your own Qobuz subscription allows. It ships no
  Qobuz app id or secret; those are scraped at runtime from the public web
  player, exactly as the original app did.");
    }
}
