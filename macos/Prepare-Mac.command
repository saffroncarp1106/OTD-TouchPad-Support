#!/bin/bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
app="$PWD/OpenTabletDriver-Touchpad.app"
if [[ ! -d "$app" ]]; then
    echo "Place this command beside OpenTabletDriver-Touchpad.app."
    exit 1
fi
chmod +x "$app/Contents/MacOS/OpenTabletDriver.UX.MacOS" "$app/Contents/MacOS/OpenTabletDriver.Daemon"
/usr/bin/codesign --force --deep --sign - "$app"
echo "Prepared. Close any other OpenTabletDriver instance, then open OpenTabletDriver-Touchpad.app."
