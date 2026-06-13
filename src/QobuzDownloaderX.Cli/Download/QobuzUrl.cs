using System.Text.RegularExpressions;

namespace QobuzDownloaderX.Cli.Download;

public enum QobuzEntityType { Album, Track, Playlist, Artist, Label, Unknown }

public sealed record QobuzEntity(QobuzEntityType Type, string Id);

/// <summary>
/// Parses a Qobuz URL or a bare "type/id" reference into an entity.
/// Handles both the streaming links (play.qobuz.com / open.qobuz.com) and the
/// localized store links (e.g. www.qobuz.com/us-en/album/.../id).
/// </summary>
public static class QobuzUrl
{
    public static QobuzEntity Parse(string input)
    {
        input = input.Trim();

        // Store links: https://www.qobuz.com/<region>/<type>/<name>/<id>
        var store = Regex.Match(input,
            @"https?://(?:www\.)?qobuz\.com/[a-z]{2}-[a-z]{2}/(?<type>album|interpreter|label|playlist|track)/(?:.*?/)?(?<id>[^/?#]+)/?$",
            RegexOptions.IgnoreCase);
        if (store.Success)
            return new QobuzEntity(MapType(store.Groups["type"].Value), store.Groups["id"].Value);

        // Streaming links: https://(play|open).qobuz.com/<type>/<id>
        var stream = Regex.Match(input,
            @"https?://(?:play|open)\.qobuz\.com/(?<type>album|track|playlist|artist|label|interpreter)/(?<id>[^/?#]+)/?$",
            RegexOptions.IgnoreCase);
        if (stream.Success)
            return new QobuzEntity(MapType(stream.Groups["type"].Value), stream.Groups["id"].Value);

        // Bare "type/id" or "type:id"
        var bare = Regex.Match(input,
            @"^(?<type>album|track|playlist|artist|label)[:/](?<id>[A-Za-z0-9]+)$",
            RegexOptions.IgnoreCase);
        if (bare.Success)
            return new QobuzEntity(MapType(bare.Groups["type"].Value), bare.Groups["id"].Value);

        return new QobuzEntity(QobuzEntityType.Unknown, input);
    }

    private static QobuzEntityType MapType(string type) => type.ToLowerInvariant() switch
    {
        "album" => QobuzEntityType.Album,
        "track" => QobuzEntityType.Track,
        "playlist" => QobuzEntityType.Playlist,
        "artist" or "interpreter" => QobuzEntityType.Artist,
        "label" => QobuzEntityType.Label,
        _ => QobuzEntityType.Unknown,
    };
}
