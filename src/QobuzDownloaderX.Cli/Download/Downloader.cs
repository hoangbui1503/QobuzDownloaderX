using System.Globalization;
using QobuzDownloaderX.Cli.Config;
using QobuzDownloaderX.Cli.Qobuz;

namespace QobuzDownloaderX.Cli.Download;

/// <summary>
/// Orchestrates downloads. Consolidates the logic that the original app spread
/// across DownloadAlbum / DownloadTrack / DownloadFile, made cross-platform.
/// </summary>
public sealed class Downloader
{
    private readonly QobuzApiClient _api;
    private readonly AppConfig _cfg;
    private readonly HttpClient _http;

    public Downloader(QobuzApiClient api, AppConfig cfg, HttpClient http)
    {
        _api = api;
        _cfg = cfg;
        _http = http;
    }

    private string AudioFormat => _cfg.QualityFormatId == "5" ? ".mp3" : ".flac";

    public async Task DownloadEntityAsync(QobuzEntity entity, CancellationToken ct)
    {
        switch (entity.Type)
        {
            case QobuzEntityType.Album:
            {
                var album = await _api.GetAlbumAsync(entity.Id, ct);
                await DownloadAlbumAsync(album, ct);
                break;
            }
            case QobuzEntityType.Track:
            {
                var track = await _api.GetTrackAsync(entity.Id, ct);
                if (track.Album?.Id == null)
                    throw new QobuzApiException("Track has no album reference.");
                var album = await _api.GetAlbumAsync(track.Album.Id, ct);
                await DownloadTrackAsync(album, track, ct, single: true);
                break;
            }
            case QobuzEntityType.Playlist:
            {
                var playlist = await _api.GetPlaylistAsync(entity.Id, ct);
                await DownloadPlaylistAsync(playlist, ct);
                break;
            }
            case QobuzEntityType.Artist:
            {
                var artist = await _api.GetArtistAsync(entity.Id, ct);
                Console.WriteLine($"Artist: {artist.Name} ({artist.Albums?.Items.Count ?? 0} albums)");
                foreach (var stub in artist.Albums?.Items ?? new List<Album>())
                {
                    if (stub.Id == null) continue;
                    var album = await _api.GetAlbumAsync(stub.Id, ct);
                    await DownloadAlbumAsync(album, ct);
                }
                break;
            }
            case QobuzEntityType.Label:
            {
                var label = await _api.GetLabelAsync(entity.Id, ct);
                Console.WriteLine($"Label: {label.Name} ({label.Albums?.Items.Count ?? 0} albums)");
                foreach (var stub in label.Albums?.Items ?? new List<Album>())
                {
                    if (stub.Id == null) continue;
                    var album = await _api.GetAlbumAsync(stub.Id, ct);
                    await DownloadAlbumAsync(album, ct);
                }
                break;
            }
            default:
                throw new QobuzApiException(
                    $"Unrecognized Qobuz reference: '{entity.Id}'. Provide an album/track/playlist/artist/label URL or id.");
        }
    }

    public async Task DownloadAlbumAsync(Album album, CancellationToken ct)
    {
        Console.WriteLine($"\n=== {NamingTemplates.SafeFilename(album.Artist?.Name)} - {album.Title} ===");

        if (!album.Streamable && _cfg.StreamableCheck)
        {
            Console.WriteLine("  Album is not streamable on this account. Skipping (disable streamable-check to force).");
            return;
        }

        int padTracks = Padding.PadTracks(album);
        int padDiscs = Padding.PadDiscs(album);
        string albumDir = BuildAlbumDir(album, padTracks, padDiscs);
        Directory.CreateDirectory(albumDir);

        string? embeddedArt = await DownloadArtworkAsync(album, albumDir, ct);

        foreach (var stub in album.Tracks?.Items ?? new List<Track>())
        {
            var track = await _api.GetTrackAsync(stub.Id.ToString(CultureInfo.InvariantCulture), ct);
            track.Album ??= album;
            await DownloadTrackToAlbumDirAsync(album, track, albumDir, padTracks, padDiscs, embeddedArt, ct);
        }

        // Goodies (booklets etc.)
        foreach (var goody in album.Goodies ?? new List<Goody>())
        {
            if (string.IsNullOrEmpty(goody.Url)) continue;
            try
            {
                string goodyPath = Path.Combine(albumDir,
                    $"{NamingTemplates.SafeFilename(album.Title)} ({goody.Id}).pdf");
                await DownloadToFileAsync(goody.Url, goodyPath, ct);
                Console.WriteLine($"  Goody downloaded: {Path.GetFileName(goodyPath)}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Goody download failed: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(embeddedArt) && System.IO.File.Exists(embeddedArt))
            TryDelete(embeddedArt);

        Console.WriteLine("  DOWNLOAD COMPLETE");
    }

    private async Task DownloadTrackAsync(Album album, Track track, CancellationToken ct, bool single)
    {
        int padTracks = Padding.PadTracks(album);
        int padDiscs = Padding.PadDiscs(album);
        string albumDir = BuildAlbumDir(album, padTracks, padDiscs);
        Directory.CreateDirectory(albumDir);
        string? embeddedArt = await DownloadArtworkAsync(album, albumDir, ct);
        await DownloadTrackToAlbumDirAsync(album, track, albumDir, padTracks, padDiscs, embeddedArt, ct);
        if (!string.IsNullOrEmpty(embeddedArt) && System.IO.File.Exists(embeddedArt))
            TryDelete(embeddedArt);
        Console.WriteLine("  DOWNLOAD COMPLETE");
    }

    private async Task DownloadPlaylistAsync(Playlist playlist, CancellationToken ct)
    {
        Console.WriteLine($"\n=== Playlist: {playlist.Name} ===");
        int padTracks = Padding.PadPlaylistTracks(playlist);
        string playlistDirName = NamingTemplates.Expand(_cfg.PlaylistTemplate, padTracks, 2,
            AudioFormat, _cfg.QualityFormatId, null, null, playlist);
        string playlistDir = Path.Combine(_cfg.DownloadFolder, playlistDirName);
        Directory.CreateDirectory(playlistDir);

        foreach (var item in playlist.Tracks?.Items ?? new List<Track>())
        {
            try
            {
                var track = await _api.GetTrackAsync(item.Id.ToString(CultureInfo.InvariantCulture), ct);
                if (track.Album?.Id == null) continue;
                var album = await _api.GetAlbumAsync(track.Album.Id, ct);
                track.Album = album;
                // Preserve the playlist position for naming.
                track.Position = item.Position;

                if (!track.Streamable && _cfg.StreamableCheck)
                {
                    Console.WriteLine($"  Track {item.Position} not streamable, skipping.");
                    continue;
                }

                string trackName = NamingTemplates.Expand(_cfg.TrackTemplate, padTracks, 2,
                    AudioFormat, _cfg.QualityFormatId, album, track, playlist);
                string filePath = CombineWithSubfolders(playlistDir, trackName) + AudioFormat;
                if (System.IO.File.Exists(filePath))
                {
                    Console.WriteLine($"  Exists, skipping: {Path.GetFileName(filePath)}");
                    continue;
                }

                string? art = await DownloadArtworkAsync(album, Path.GetDirectoryName(filePath)!, ct);
                await StreamTrackToFileAsync(album, track, filePath, art, ct);
                if (!string.IsNullOrEmpty(art) && System.IO.File.Exists(art)) TryDelete(art);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Track failed: {ex.Message}");
            }
        }
        Console.WriteLine("  DOWNLOAD COMPLETE");
    }

    private async Task DownloadTrackToAlbumDirAsync(Album album, Track track, string albumDir,
        int padTracks, int padDiscs, string? embeddedArt, CancellationToken ct)
    {
        if (!track.Streamable && _cfg.StreamableCheck)
        {
            Console.WriteLine($"  Track {track.TrackNumber} not streamable, skipping.");
            return;
        }

        string trackName = NamingTemplates.Expand(_cfg.TrackTemplate, padTracks, padDiscs,
            AudioFormat, _cfg.QualityFormatId, album, track, null);

        string targetDir = albumDir;
        if (album.MediaCount > 1)
            targetDir = Path.Combine(albumDir,
                "CD " + track.MediaNumber.ToString(CultureInfo.InvariantCulture).PadLeft(padDiscs, '0'));
        Directory.CreateDirectory(targetDir);

        string filePath = CombineWithSubfolders(targetDir, trackName) + AudioFormat;
        if (System.IO.File.Exists(filePath))
        {
            Console.WriteLine($"  Exists, skipping: {Path.GetFileName(filePath)}");
            return;
        }

        await StreamTrackToFileAsync(album, track, filePath, embeddedArt, ct);
    }

    private async Task StreamTrackToFileAsync(Album album, Track track, string filePath,
        string? embeddedArt, CancellationToken ct)
    {
        string label = string.IsNullOrEmpty(track.Version)
            ? track.Title ?? ""
            : $"{track.Title?.TrimEnd()} ({track.Version})";
        Console.Write($"  Downloading {track.TrackNumber:00} {label} ... ");

        var file = await _api.GetTrackFileUrlAsync(
            track.Id.ToString(CultureInfo.InvariantCulture), _cfg.QualityFormatId, ct);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        string tempFile = filePath + ".part";
        await DownloadToFileAsync(file.Url!, tempFile, ct);

        if (_cfg.FixFlacMd5 && AudioFormat == ".flac")
            FlacMd5.TryFix(tempFile);

        try
        {
            Tagger.WriteTags(tempFile, embeddedArt, album, track, _cfg.Tags);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n  Tagging failed ({ex.Message}); keeping untagged file.");
        }

        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        System.IO.File.Move(tempFile, filePath);
        Console.WriteLine("DONE");
    }

    private async Task<string?> DownloadArtworkAsync(Album album, string dir, CancellationToken ct)
    {
        string? large = album.Image?.Large;
        if (string.IsNullOrEmpty(large)) return null;
        Directory.CreateDirectory(dir);

        string coverPath = Path.Combine(dir, "Cover.jpg");
        string embeddedPath = Path.Combine(dir, $"{_cfg.EmbeddedArtSize}.jpg");

        try
        {
            if (!System.IO.File.Exists(coverPath))
                await DownloadToFileAsync(large.Replace("_600", "_" + _cfg.SavedArtSize), coverPath, ct);
            if (!System.IO.File.Exists(embeddedPath))
                await DownloadToFileAsync(large.Replace("_600", "_" + _cfg.EmbeddedArtSize), embeddedPath, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Artwork download failed: {ex.Message}");
            return System.IO.File.Exists(embeddedPath) ? embeddedPath : null;
        }
        return embeddedPath;
    }

    private string BuildAlbumDir(Album album, int padTracks, int padDiscs)
    {
        string rel = NamingTemplates.Expand(_cfg.AlbumTemplate, padTracks, padDiscs,
            AudioFormat, _cfg.QualityFormatId, album, null, null);
        return Path.Combine(_cfg.DownloadFolder, rel);
    }

    private static string CombineWithSubfolders(string baseDir, string templatedName)
    {
        // The track template may itself contain folder separators.
        string normalized = templatedName.Replace('/', Path.DirectorySeparatorChar)
                                          .Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(baseDir, normalized);
    }

    private async Task DownloadToFileAsync(string url, string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, ct);
    }

    private static void TryDelete(string path)
    {
        try { System.IO.File.Delete(path); } catch { /* ignore */ }
    }
}
