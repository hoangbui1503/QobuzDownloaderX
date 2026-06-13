# QobuzDownloaderX — CLI (macOS / Linux / Windows)

A cross-platform command-line port of QobuzDownloaderX. The original app is a
Windows-only WinForms program; this version is a **.NET 8 console app** that
runs natively on **macOS** (Apple Silicon and Intel), Linux and Windows.

It reuses the original logic — login, info lookup, stream-URL signing, naming
templates, metadata tagging and artwork embedding — but replaces every
Windows-only dependency (WinForms, `System.Drawing`, `ZetaLongPaths`,
`System.Deployment`, the closed-source `QopenAPI` DLL, the P/Invoke calls) with
portable code:

| Original (Windows) | CLI port (cross-platform) |
| --- | --- |
| WinForms UI | Console commands (`login`, `get`, `config`) |
| `QopenAPI.dll` | `Qobuz/QobuzApiClient.cs` + `BundleParser.cs` (HttpClient) |
| `taglib-sharp` (net45) | `TagLibSharp` NuGet (netstandard) |
| `ZetaLongPaths` | `System.IO` (no MAX_PATH limit on Unix) |
| `Properties.Settings` | `~/.config/QobuzDownloaderX/config.json` |
| `flac.exe` via `cmd.exe` | `flac` CLI if present, else skipped |

> This tool downloads only what your own Qobuz subscription allows. It ships **no**
> Qobuz app id or secret — those are scraped at runtime from the public web
> player, exactly as the original app did. You need a paid Qobuz account.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (`brew install --cask dotnet-sdk` on macOS)
- Optional: `flac` for fixing unset FLAC MD5 signatures (`brew install flac`)

## Build & run

```bash
cd src/QobuzDownloaderX.Cli
dotnet build -c Release
# binary: bin/Release/net8.0/qobuz-dl-x   (run via `dotnet bin/Release/net8.0/qobuz-dl-x.dll`)
```

Or publish a self-contained macOS binary (no .NET needed on the target):

```bash
# Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
# Intel Macs
dotnet publish -c Release -r osx-x64   --self-contained -p:PublishSingleFile=true
```

The single-file executable is written to
`bin/Release/net8.0/<rid>/publish/qobuz-dl-x`.

## Usage

```bash
# Log in (prompts for password; credentials saved to the config file)
qobuz-dl-x login --email me@example.com

# …or log in with an existing user_auth_token
qobuz-dl-x login --token 6Hx...your_token...

# Download (album / track / playlist / artist / label) by URL or id
qobuz-dl-x get https://open.qobuz.com/album/0060254735907 --quality max
qobuz-dl-x get album/0060254735907 track/12345678 --out ~/Music/Qobuz

# Inspect / change defaults
qobuz-dl-x config show
qobuz-dl-x config set --quality hires --out ~/Music/Qobuz
```

### Quality values

| Value | format_id | Meaning |
| --- | --- | --- |
| `mp3`  | 5  | MP3 320 kbps |
| `flac` | 6  | FLAC 16-bit / 44.1 kHz (CD) |
| `hires`| 7  | FLAC 24-bit up to 96 kHz |
| `max`  | 27 | FLAC 24-bit up to 192 kHz |

### Naming templates

The same `%Placeholder%` tokens as the original are supported, e.g.
`%ArtistName%`, `%AlbumTitle%`, `%TrackNumber%`, `%TrackTitle%`,
`%PlaylistTitle%`, `%Year%`, `%Label%`, `%Format%`, `%FormatWithQuality%`, the
parental-advisory tokens, and so on. Use `/` to create sub-folders:

```bash
qobuz-dl-x config set --album-template "%ArtistName%/%AlbumTitle% (%Year%)"
```

## Notes

- Multi-disc albums are placed in `CD 01`, `CD 02`, … sub-folders, matching the
  original behaviour.
- Cover art is saved as `Cover.jpg` and embedded into each track.
- If a track already exists on disk it is skipped.
