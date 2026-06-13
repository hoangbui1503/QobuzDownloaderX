#!/bin/bash
# Builds QobuzDownloaderX.app — a double-clickable macOS app that bundles the
# self-contained .NET CLI (no .NET install required on the target Mac).
#
# Usage:
#   packaging/macos/build-app.sh [arm64|x64|both]   (default: arm64)
#
# Output: packaging/macos/dist/QobuzDownloaderX.app  (+ a .zip next to it)
set -euo pipefail

ARCH="${1:-arm64}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJ="$REPO_ROOT/src/QobuzDownloaderX.Cli/QobuzDownloaderX.Cli.csproj"
DIST="$SCRIPT_DIR/dist"
APP="$DIST/QobuzDownloaderX.app"

publish() { # $1 = rid
    dotnet publish "$PROJ" -c Release -r "$1" --self-contained \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$DIST/_publish_$1"
}

rm -rf "$DIST"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "$SCRIPT_DIR/Info.plist" "$APP/Contents/Info.plist"
cp "$SCRIPT_DIR/launcher.sh" "$APP/Contents/MacOS/QobuzDownloaderX"
chmod +x "$APP/Contents/MacOS/QobuzDownloaderX"

case "$ARCH" in
    arm64) publish osx-arm64; cp "$DIST/_publish_osx-arm64/qobuz-dl-x" "$APP/Contents/MacOS/qobuz-dl-x" ;;
    x64)   publish osx-x64;   cp "$DIST/_publish_osx-x64/qobuz-dl-x"   "$APP/Contents/MacOS/qobuz-dl-x" ;;
    both)
        publish osx-arm64; publish osx-x64
        # Stitch a universal binary if running on macOS (lipo available).
        if command -v lipo >/dev/null 2>&1; then
            lipo -create -output "$APP/Contents/MacOS/qobuz-dl-x" \
                "$DIST/_publish_osx-arm64/qobuz-dl-x" "$DIST/_publish_osx-x64/qobuz-dl-x"
        else
            echo "lipo not found; shipping arm64 + x64 with an arch-picking wrapper"
            mv "$DIST/_publish_osx-arm64/qobuz-dl-x" "$APP/Contents/MacOS/qobuz-dl-x-arm64"
            mv "$DIST/_publish_osx-x64/qobuz-dl-x"   "$APP/Contents/MacOS/qobuz-dl-x-x64"
            cat > "$APP/Contents/MacOS/qobuz-dl-x" <<'WRAP'
#!/bin/bash
H="$(cd "$(dirname "$0")" && pwd)"
if [ "$(uname -m)" = "arm64" ]; then exec "$H/qobuz-dl-x-arm64" "$@"; else exec "$H/qobuz-dl-x-x64" "$@"; fi
WRAP
            chmod +x "$APP/Contents/MacOS/qobuz-dl-x"
        fi
        ;;
    *) echo "Unknown arch: $ARCH (use arm64|x64|both)"; exit 1 ;;
esac

chmod +x "$APP/Contents/MacOS/qobuz-dl-x"* 2>/dev/null || true
rm -rf "$DIST"/_publish_*

cp "$SCRIPT_DIR/ĐỌC TRƯỚC.txt" "$DIST/ĐỌC TRƯỚC.txt" 2>/dev/null || true

( cd "$DIST" && zip -r -y -q "QobuzDownloaderX-macos.zip" "QobuzDownloaderX.app" "ĐỌC TRƯỚC.txt" 2>/dev/null || \
  zip -r -y -q "QobuzDownloaderX-macos.zip" "QobuzDownloaderX.app" )

echo "Done -> $DIST/QobuzDownloaderX.app"
echo "Zip  -> $DIST/QobuzDownloaderX-macos.zip"
