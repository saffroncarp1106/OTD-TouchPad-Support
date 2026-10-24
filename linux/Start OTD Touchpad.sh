#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
source "$folder/linux-common.sh"
if (( EUID == 0 )); then
    printf 'Start this app as your normal desktop user, after running Prepare Linux.sh.\n' >&2
    exit 1
fi
# A source download contains launch scripts, but no executable in linux/.
if [[ ! -x "$folder/OpenTabletDriver.UX.Gtk" ]]; then
    output=${OTD_LINUX_OUTPUT:-"$folder/../artifacts/OTD-Touchpad-Linux-x64-v0.0.2"}
    if [[ ! -x "$output/OpenTabletDriver.UX.Gtk" ]]; then
        if [[ ! -f "$folder/build-linux.sh" ]]; then
            printf 'Incomplete portable release: OpenTabletDriver.UX.Gtk is missing. Extract the complete binary archive.\n' >&2
            exit 1
        fi
        if ! command -v "${OTD_DOTNET:-dotnet}" >/dev/null; then
            printf 'This is a source download and has not been built.\n' >&2
            otd_dependency_hint >&2
            printf 'Then run: bash "%s/build-linux.sh"\nOr download the self-contained binary release.\n' "$folder" >&2
            exit 1
        fi
        bash "$folder/build-linux.sh"
    fi
    exec bash "$output/Start OTD Touchpad.sh" "$@"
fi
cd -- "$folder"
otd_check_libraries
if [[ -z ${DISPLAY:-} && -z ${WAYLAND_DISPLAY:-} ]]; then
    printf 'Start OTD from your local graphical desktop session.\n' >&2
    exit 1
fi
if [[ ! -r /dev/uinput || ! -w /dev/uinput ]]; then
    bash "$folder/Prepare Linux.sh"
fi
if systemctl --user --quiet is-active opentabletdriver.service 2>/dev/null; then
    printf 'An installed OTD service is running. Stop it first: systemctl --user stop opentabletdriver.service\n' >&2
    exit 1
fi
if [[ ! -r /dev/uinput || ! -w /dev/uinput ]]; then
    printf 'uinput access is still unavailable. Log out and back in after setup.\n' >&2
    exit 1
fi
# Userdata beside the app isolates this fork from existing OTD settings.
mkdir -p "$folder/userdata" "$folder/logs"
exec "$folder/OpenTabletDriver.UX.Gtk" --skipupdate "$@" > >(tee -a "$folder/logs/app.log") 2>&1
