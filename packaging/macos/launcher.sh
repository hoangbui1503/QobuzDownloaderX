#!/bin/bash
# CFBundleExecutable for QobuzDownloaderX.app.
# A .app's main executable is launched without a visible terminal, so we open
# Terminal.app and run the real (self-contained .NET) binary there in the
# friendly interactive menu mode.

HERE="$(cd "$(dirname "$0")" && pwd)"
BIN="$HERE/qobuz-dl-x"

# Best-effort: clear the quarantine flag so the inner binary runs without the
# "cannot be opened" Gatekeeper prompt. (The .app itself may still need a
# one-time right-click -> Open the first time — see the included README.)
xattr -dr com.apple.quarantine "$BIN" 2>/dev/null
chmod +x "$BIN" 2>/dev/null

/usr/bin/osascript <<EOF
tell application "Terminal"
    activate
    do script "clear; '$BIN' menu; echo; echo '--- Bạn có thể đóng cửa sổ này ---'"
end tell
EOF
