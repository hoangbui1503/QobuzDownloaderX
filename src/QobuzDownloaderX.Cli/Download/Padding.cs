using QobuzDownloaderX.Cli.Qobuz;

namespace QobuzDownloaderX.Cli.Download;

/// <summary>Port of the original PaddingNumbers helper.</summary>
public static class Padding
{
    private static int PadFor(int count)
    {
        if (count <= 0) return 2;
        int digits = (int)Math.Floor(Math.Log10(count) + 1);
        return digits <= 1 ? 2 : digits;
    }

    public static int PadTracks(Album album) => PadFor(album.TracksCount);
    public static int PadDiscs(Album album) => PadFor(album.MediaCount);
    public static int PadPlaylistTracks(Playlist playlist) => PadFor(playlist.TracksCount);
}
