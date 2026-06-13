using System.Text.Json.Serialization;

namespace QobuzDownloaderX.Cli.Qobuz;

// The Qobuz API returns snake_case JSON. The JsonSerializerOptions in
// QobuzApiClient set PropertyNamingPolicy = SnakeCaseLower so most members
// map automatically; [JsonPropertyName] is only used for the exceptions.

public sealed class LoginResponse
{
    public string? UserAuthToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Status { get; set; }
}

public sealed class UserInfo
{
    public long Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public Credential? Credential { get; set; }
}

public sealed class Credential
{
    public string? Description { get; set; }
    public CredentialParameters? Parameters { get; set; }
}

public sealed class CredentialParameters
{
    public string? Label { get; set; }
    public string? ShortLabel { get; set; }
}

public sealed class Image
{
    public string? Small { get; set; }
    public string? Thumbnail { get; set; }
    public string? Large { get; set; }
    public string? Back { get; set; }
}

public sealed class NamedValue
{
    public string? Name { get; set; }
}

public sealed class Artist
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public List<string>? Roles { get; set; }
    public AlbumList? Albums { get; set; }
}

public sealed class Goody
{
    public long Id { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
}

public sealed class Album
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Version { get; set; }
    public string? Url { get; set; }
    public string? Copyright { get; set; }
    public string? Upc { get; set; }
    public string? Description { get; set; }

    [JsonPropertyName("release_date_original")]
    public string? ReleaseDateOriginal { get; set; }

    public string? ProductType { get; set; }
    public int MaximumBitDepth { get; set; }
    public double MaximumSamplingRate { get; set; }
    public bool ParentalWarning { get; set; }
    public bool Streamable { get; set; }
    public int MediaCount { get; set; }
    public int TracksCount { get; set; }

    public Image? Image { get; set; }
    public NamedValue? Genre { get; set; }
    public NamedValue? Label { get; set; }
    public NamedValue? Composer { get; set; }
    public Artist? Artist { get; set; }
    public List<Artist>? Artists { get; set; }
    public List<Goody>? Goodies { get; set; }
    public TrackList? Tracks { get; set; }
}

public sealed class Track
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Version { get; set; }
    public string? Isrc { get; set; }
    public int TrackNumber { get; set; }
    public int MediaNumber { get; set; }
    public int Position { get; set; } // used by playlists
    public int Duration { get; set; }
    public int MaximumBitDepth { get; set; }
    public double MaximumSamplingRate { get; set; }
    public bool ParentalWarning { get; set; }
    public bool Streamable { get; set; }

    public NamedValue? Performer { get; set; }
    public NamedValue? Composer { get; set; }
    public Album? Album { get; set; }
}

public sealed class TrackList
{
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public List<Track> Items { get; set; } = new();
}

public sealed class AlbumList
{
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public List<Album> Items { get; set; } = new();
}

public sealed class Playlist
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int TracksCount { get; set; }
    public TrackList? Tracks { get; set; }
}

public sealed class Label
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public AlbumList? Albums { get; set; }
}

public sealed class Favorites
{
    public AlbumList? Albums { get; set; }
    public TrackList? Tracks { get; set; }
    public ArtistList? Artists { get; set; }
}

public sealed class ArtistList
{
    public int Total { get; set; }
    public List<Artist> Items { get; set; } = new();
}

public sealed class FileUrl
{
    public string? Url { get; set; }
    public int FormatId { get; set; }
    public string? MimeType { get; set; }
    public int BitDepth { get; set; }
    public double SamplingRate { get; set; }
    public bool Sample { get; set; }
}
