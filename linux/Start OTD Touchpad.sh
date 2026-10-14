#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd -- "$folder"
if (( EUID == 0 )); then
    printf 'Start this app as your normal desktop user, after running Prepare Linux.sh.\n' >&2
    exit 1
fi
if [[ ! -r /dev/uinput || ! -w /dev/uinput ]]; then
    bash "$folder/Prepare Linux.sh"
fi
if [[ ! -r /dev/uinput || ! -w /dev/uinput ]]; then
    printf 'uinput access is still unavailable. Log out and back in after setup.\n' >&2
    exit 1
fi
# Userdata beside the app isolates this fork from existing OTD settings.
mkdir -p "$folder/userdata" "$folder/logs"
exec "$folder/OpenTabletDriver.UX.Gtk" --skipupdate "$@" > >(tee -a "$folder/logs/app.log") 2>&1
