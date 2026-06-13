using System.Globalization;
using System.Text.RegularExpressions;
using QobuzDownloaderX.Cli.Qobuz;

namespace QobuzDownloaderX.Cli.Download;

/// <summary>
/// Cross-platform port of RenameTemplates. Expands the %Placeholder% tokens the
/// original app supported. A forward slash in a template is treated as a folder
/// separator (replacing the Windows backslash behaviour of the original).
/// </summary>
public static class NamingTemplates
{
    private static readonly char[] ExtraInvalid = { '<', '>', ':', '"', '|', '?', '*' };

    public static string SafeFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename)) return "";
        string trimmed = filename.TrimEnd().TrimEnd('.');
        var invalid = Path.GetInvalidFileNameChars().Concat(ExtraInvalid).Distinct().ToArray();
        return string.Join("_", trimmed.Split(invalid, StringSplitOptions.None));
    }

    public static string Expand(string template, int paddedTrackLength, int paddedDiscLength,
        string fileFormat, string formatId, Album? album, Track? track, Playlist? playlist)
    {
        // Preserve folder separators across the safe-filename pass.
        const string Sep = "{__sep__}";
        string result = template.Replace("/", Sep).Replace("\\", Sep);

        string fmt = fileFormat.ToUpperInvariant().TrimStart('.');

        if (track != null)
        {
            ReplacePa(ref result, "Track", track.ParentalWarning);
            Replace(ref result, "%TrackID%", track.Id.ToString(CultureInfo.InvariantCulture));
            Replace(ref result, "%TrackArtist%", track.Performer?.Name);
            Replace(ref result, "%TrackComposer%", track.Composer?.Name);
            Replace(ref result, "%TrackTitle%", TitleWithVersion(track.Title, track.Version));
            Replace(ref result, "%TrackNumber%", track.TrackNumber.ToString(CultureInfo.InvariantCulture).PadLeft(paddedTrackLength, '0'));
            Replace(ref result, "%TrackPosition%", track.Position.ToString(CultureInfo.InvariantCulture).PadLeft(paddedTrackLength, '0'));
            Replace(ref result, "%ISRC%", track.Isrc);
            Replace(ref result, "%TrackBitDepth%", track.MaximumBitDepth.ToString(CultureInfo.InvariantCulture));
            Replace(ref result, "%TrackSampleRate%", Num(track.MaximumSamplingRate));
            Replace(ref result, "%TrackFormat%", fmt);
            Replace(ref result, "%TrackFormatWithQuality%", QualityLabel(fmt, formatId, track.MaximumBitDepth, track.MaximumSamplingRate));
            Replace(ref result, "%TrackFormatWithHiResQuality%", QualityLabel(fmt, formatId, track.MaximumBitDepth, track.MaximumSamplingRate));
        }

        if (album != null)
        {
            Replace(ref result, "%AlbumID%", album.Id);
            Replace(ref result, "%AlbumURL%", album.Url);
            Replace(ref result, "%AlbumGenre%", album.Genre?.Name);
            Replace(ref result, "%AlbumComposer%", album.Composer?.Name);
            Replace(ref result, "%AlbumTitle%", TitleWithVersion(album.Title, album.Version));
            Replace(ref result, "%Label%", CollapseSpaces(album.Label?.Name));
            Replace(ref result, "%Copyright%", album.Copyright);
            Replace(ref result, "%UPC%", album.Upc);
            Replace(ref result, "%ReleaseDate%", album.ReleaseDateOriginal);
            Replace(ref result, "%Year%", Year(album.ReleaseDateOriginal));
            Replace(ref result, "%ReleaseType%", TitleCase(album.ProductType));
            Replace(ref result, "%BitDepth%", album.MaximumBitDepth.ToString(CultureInfo.InvariantCulture));
            Replace(ref result, "%SampleRate%", Num(album.MaximumSamplingRate));
            Replace(ref result, "%Format%", fmt);
            Replace(ref result, "%ArtistName%", AlbumArtistName(album));
            Replace(ref result, "%FormatWithQuality%", QualityLabel(fmt, formatId, album.MaximumBitDepth, album.MaximumSamplingRate));
            Replace(ref result, "%FormatWithHiResQuality%", QualityLabel(fmt, formatId, album.MaximumBitDepth, album.MaximumSamplingRate));
            ReplacePa(ref result, "Album", album.ParentalWarning);
        }

        if (playlist != null)
        {
            Replace(ref result, "%PlaylistID%", playlist.Id.ToString(CultureInfo.InvariantCulture));
            Replace(ref result, "%PlaylistTitle%", playlist.Name);
            Replace(ref result, "%Format%", fmt);
            Replace(ref result, "%FormatWithQuality%", fmt);
            Replace(ref result, "%FormatWithHiResQuality%", fmt);
        }

        result = result.Replace(Sep, Path.DirectorySeparatorChar.ToString());
        result = Regex.Replace(result, @"[ \t]+", " ");
        result = result.Replace(" " + Path.DirectorySeparatorChar, Path.DirectorySeparatorChar.ToString());
        return result.Trim();
    }

    private static void Replace(ref string s, string token, string? value)
    {
        if (!s.Contains(token)) return;
        s = s.Replace(token, SafeFilename(value ?? ""));
    }

    private static void ReplacePa(ref string s, string prefix, bool explicitContent)
    {
        // Mirror the original parental-advisory token set.
        (string token, string ex, string cl)[] map =
        {
            ($"%{prefix}PA%", "Explicit", "Clean"),
            ($"%{prefix}PAShort%", "E", "C"),
            ($"%{prefix}PAifEx%", "Explicit", ""),
            ($"%{prefix}PAifExShort%", "E", ""),
            ($"%{prefix}PAifCl%", "", "Clean"),
            ($"%{prefix}PAifClShort%", "", "C"),
            ($"%{prefix}PAEnclosed%", "(Explicit)", "(Clean)"),
            ($"%{prefix}PAEnclosed[]%", "[Explicit]", "[Clean]"),
            ($"%{prefix}PAEnclosedShort%", "(E)", "(C)"),
            ($"%{prefix}PAEnclosedShort[]%", "[E]", "[C]"),
            ($"%{prefix}PAifExEnclosed%", "(Explicit)", ""),
            ($"%{prefix}PAifExEnclosed[]%", "[Explicit]", ""),
            ($"%{prefix}PAifClEnclosed%", "", "(Clean)"),
            ($"%{prefix}PAifClEnclosed[]%", "", "[Clean]"),
        };
        foreach (var (token, ex, cl) in map)
            Replace(ref s, token, explicitContent ? ex : cl);
    }

    private static string TitleWithVersion(string? title, string? version) =>
        string.IsNullOrEmpty(version) ? (title ?? "") : $"{(title ?? "").TrimEnd()} ({version})";

    private static string AlbumArtistName(Album album)
    {
        var mains = album.Artists?.Where(a => a.Roles?.Contains("main-artist") == true).ToList();
        if (mains is { Count: > 1 })
        {
            string allButLast = string.Join(", ", mains.Take(mains.Count - 1).Select(a => a.Name));
            return $"{allButLast} & {mains.Last().Name}";
        }
        return album.Artist?.Name ?? mains?.FirstOrDefault()?.Name ?? "";
    }

    private static string QualityLabel(string fmt, string formatId, int bitDepth, double sampleRate)
    {
        // MP3 / plain FLAC: no quality suffix.
        if (formatId is "5" or "6") return fmt;
        if (bitDepth <= 16) return $"{fmt} ({bitDepth}bit-{Num(sampleRate)}kHz)";
        return $"{fmt} ({bitDepth}bit-{Num(sampleRate)}kHz)";
    }

    private static string Num(double value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private static string CollapseSpaces(string? s) =>
        string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, @"\s+", " ");

    private static string Year(string? releaseDate) =>
        !string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4 ? releaseDate.Substring(0, 4) : "";

    private static string TitleCase(string? s) =>
        string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
}
