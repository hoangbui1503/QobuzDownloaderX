using System.Text.Json;
using System.Text.Json.Serialization;

namespace QobuzDownloaderX.Cli.Config;

/// <summary>
/// Persisted configuration + saved credentials. Replaces the Windows-only
/// Properties.Settings used by the original WinForms app. Stored as JSON under
/// the platform config directory (e.g. ~/.config/QobuzDownloaderX on macOS/Linux).
/// </summary>
public sealed class AppConfig
{
    // Credentials
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
    public string? UserAuthToken { get; set; }
    public string? UserId { get; set; }
    public string? DisplayName { get; set; }

    // Download preferences
    public string DownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "Qobuz Downloads");

    /// <summary>Qobuz format id: 5=MP3 320, 6=FLAC 16/44.1, 7=FLAC 24/96, 27=FLAC 24/192.</summary>
    public string QualityFormatId { get; set; } = "27";

    public string AlbumTemplate { get; set; } = "%ArtistName%/%AlbumTitle%";
    public string TrackTemplate { get; set; } = "%TrackNumber% %TrackTitle%";
    public string PlaylistTemplate { get; set; } = "%PlaylistTitle%";

    public string EmbeddedArtSize { get; set; } = "600";
    public string SavedArtSize { get; set; } = "600";

    public bool StreamableCheck { get; set; } = true;
    public bool FixFlacMd5 { get; set; } = true;

    public TagOptions Tags { get; set; } = new();

    // ---- persistence ----

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ConfigDirectory
    {
        get
        {
            string baseDir = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "QobuzDownloaderX");
        }
    }

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not read config ({ex.Message}); using defaults.");
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    public bool HasCredentials =>
        !string.IsNullOrEmpty(AppId) &&
        !string.IsNullOrEmpty(UserAuthToken);
}

public sealed class TagOptions
{
    public bool AlbumTag { get; set; } = true;
    public bool ArtistTag { get; set; } = true;
    public bool AlbumArtistTag { get; set; } = true;
    public bool TrackTitleTag { get; set; } = true;
    public bool TrackNumberTag { get; set; } = true;
    public bool TotalTracksTag { get; set; } = true;
    public bool DiscTag { get; set; } = true;
    public bool TotalDiscsTag { get; set; } = true;
    public bool GenreTag { get; set; } = true;
    public bool ComposerTag { get; set; } = true;
    public bool IsrcTag { get; set; } = true;
    public bool UpcTag { get; set; } = true;
    public bool CopyrightTag { get; set; } = true;
    public bool YearTag { get; set; } = true;
    public bool ExplicitTag { get; set; } = true;
    public bool LabelTag { get; set; } = true;
    public bool MediaTypeTag { get; set; } = true;
    public bool ImageTag { get; set; } = true;
    public bool CommentTag { get; set; } = false;
    public string CommentText { get; set; } = "";
}
