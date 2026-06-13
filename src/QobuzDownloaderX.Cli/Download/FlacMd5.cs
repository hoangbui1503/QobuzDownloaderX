using System.Diagnostics;

namespace QobuzDownloaderX.Cli.Download;

/// <summary>
/// Cross-platform replacement for the original FixMD5 (which shelled out to
/// flac.exe via cmd.exe on Windows). If the `flac` CLI is available on PATH it
/// re-encodes in place to populate the streaminfo MD5; otherwise it is a no-op.
/// On macOS: `brew install flac`.
/// </summary>
public static class FlacMd5
{
    private static bool? _flacAvailable;

    public static void TryFix(string flacFile)
    {
        if (!FlacAvailable()) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "flac",
                ArgumentList = { "-f", "-8", "-s", flacFile },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static bool FlacAvailable()
    {
        if (_flacAvailable.HasValue) return _flacAvailable.Value;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "flac",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            _flacAvailable = proc is { ExitCode: 0 };
        }
        catch
        {
            _flacAvailable = false;
        }
        return _flacAvailable.Value;
    }
}
