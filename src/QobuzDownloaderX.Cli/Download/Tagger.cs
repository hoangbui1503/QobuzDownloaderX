using System.Globalization;
using System.Text.RegularExpressions;
using QobuzDownloaderX.Cli.Config;
using QobuzDownloaderX.Cli.Qobuz;
using TagLib;
using TagLib.Id3v2;
using File = TagLib.File;

namespace QobuzDownloaderX.Cli.Download;

/// <summary>Cross-platform port of TagFile using TagLibSharp.</summary>
public static class Tagger
{
    public static void WriteTags(string filePath, string? artworkPath, Album album, Track track, TagOptions opts)
    {
        using var file = File.Create(filePath);
        bool isFlac = filePath.EndsWith(".flac", StringComparison.OrdinalIgnoreCase);

        if (isFlac)
        {
            if (file.GetTag(TagTypes.Xiph, true) is TagLib.Ogg.XiphComment xiph)
                SetFlacTags(xiph, album, track, opts);
        }
        else
        {
            if (file.GetTag(TagTypes.Id3v2, true) is TagLib.Id3v2.Tag id3)
                SetMp3Tags(id3, album, track, opts);
        }

        SetCommonTags(file, album, track, opts);
        SetAlbumArtists(file, album, opts);
        EmbedArtwork(file, artworkPath, opts);

        file.Save();
    }

    private static void SetFlacTags(TagLib.Ogg.XiphComment tags, Album album, Track track, TagOptions o)
    {
        if (o.YearTag && album.ReleaseDateOriginal != null) tags.SetField("DATE", album.ReleaseDateOriginal);
        if (o.IsrcTag && track.Isrc != null) tags.SetField("ISRC", track.Isrc);
        if (o.MediaTypeTag && album.ProductType != null) tags.SetField("MEDIATYPE", album.ProductType.ToUpperInvariant());
        if (o.UpcTag && album.Upc != null) tags.SetField("BARCODE", album.Upc);
        if (o.LabelTag && album.Label?.Name != null) tags.SetField("LABEL", Regex.Replace(album.Label.Name, @"\s+", " "));
        if (o.ExplicitTag) tags.SetField("ITUNESADVISORY", track.ParentalWarning ? "1" : "0");
        if (o.CommentTag && !string.IsNullOrEmpty(o.CommentText))
            tags.SetField("COMMENT", ExpandComment(o.CommentText, album));
    }

    private static void SetMp3Tags(TagLib.Id3v2.Tag tag, Album album, Track track, TagOptions o)
    {
        if (o.UpcTag && album.Upc != null) UserTextInformationFrame.Get(tag, "BARCODE", true).Text = new[] { album.Upc };
        if (o.ExplicitTag) UserTextInformationFrame.Get(tag, "ITUNESADVISORY", true).Text = new[] { track.ParentalWarning ? "1" : "0" };
        if (o.YearTag && TryYear(album.ReleaseDateOriginal, out uint year)) tag.Year = year;
        if (o.IsrcTag && track.Isrc != null) tag.SetTextFrame("TSRC", track.Isrc);
        if (o.LabelTag && album.Label?.Name != null) tag.SetTextFrame("TPUB", Regex.Replace(album.Label.Name, @"\s+", " "));
        if (o.MediaTypeTag && album.ProductType != null) tag.SetTextFrame("TMED", album.ProductType.ToUpperInvariant());
        if (o.CommentTag && !string.IsNullOrEmpty(o.CommentText))
            tag.Comment = ExpandComment(o.CommentText, album);
    }

    private static void SetCommonTags(File file, Album album, Track track, TagOptions o)
    {
        if (o.TrackTitleTag) file.Tag.Title = string.IsNullOrEmpty(track.Version)
            ? track.Title : $"{track.Title?.TrimEnd()} ({track.Version})";
        if (o.ArtistTag && track.Performer?.Name != null) file.Tag.Performers = new[] { track.Performer.Name };
        if (o.GenreTag && album.Genre?.Name != null) file.Tag.Genres = new[] { album.Genre.Name };
        if (o.AlbumTag) file.Tag.Album = string.IsNullOrEmpty(album.Version)
            ? album.Title : $"{album.Title?.TrimEnd()} ({album.Version})";
        if (o.ComposerTag && track.Composer?.Name != null) file.Tag.Composers = new[] { track.Composer.Name };
        if (o.TrackNumberTag) file.Tag.Track = (uint)Math.Max(0, track.TrackNumber);
        if (o.TotalTracksTag) file.Tag.TrackCount = (uint)Math.Max(0, album.TracksCount);
        if (o.DiscTag) file.Tag.Disc = (uint)Math.Max(0, track.MediaNumber);
        if (o.TotalDiscsTag) file.Tag.DiscCount = (uint)Math.Max(0, album.MediaCount);
        if (o.CopyrightTag && album.Copyright != null) file.Tag.Copyright = album.Copyright;
    }

    private static void SetAlbumArtists(File file, Album album, TagOptions o)
    {
        if (!o.AlbumArtistTag) return;
        var mains = album.Artists?.Where(a => a.Roles?.Contains("main-artist") == true).ToList();
        string albumArtist = mains is { Count: > 1 }
            ? string.Join(", ", mains.Select(a => a.Name))
            : album.Artist?.Name ?? mains?.FirstOrDefault()?.Name ?? "";
        if (!string.IsNullOrEmpty(albumArtist))
            file.Tag.AlbumArtists = new[] { albumArtist };
    }

    private static void EmbedArtwork(File file, string? artworkPath, TagOptions o)
    {
        if (!o.ImageTag || string.IsNullOrEmpty(artworkPath) || !System.IO.File.Exists(artworkPath)) return;
        try
        {
            var pic = new AttachedPictureFrame
            {
                TextEncoding = StringType.Latin1,
                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                Type = PictureType.FrontCover,
                Data = ByteVector.FromPath(artworkPath),
            };
            file.Tag.Pictures = new IPicture[] { pic };
        }
        catch
        {
            // Non-fatal: keep the audio even if artwork embedding fails.
        }
    }

    private static string ExpandComment(string text, Album album) => text
        .Replace("%description%", album.Description ?? "")
        .Replace("<br/>", Environment.NewLine)
        .Replace("<br />", Environment.NewLine);

    private static bool TryYear(string? releaseDate, out uint year)
    {
        year = 0;
        return releaseDate is { Length: >= 4 } &&
               uint.TryParse(releaseDate.Substring(0, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out year);
    }
}
