#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
source "$folder/linux-common.sh"
otd_check_libraries
if (( EUID != 0 )); then
    if [[ -t 0 ]]; then
        exec sudo bash "$folder/Prepare Linux.sh"
    else
        exec pkexec bash "$folder/Prepare Linux.sh"
    fi
fi
for utility in modprobe udevadm install; do
    command -v "$utility" >/dev/null || { printf 'Missing required command: %s\n' "$utility"; exit 1; }
done
install -m 0644 "$folder/70-otd-touchpad.rules" /etc/udev/rules.d/70-otd-touchpad.rules
modprobe uinput
# Load uinput automatically at boot.
install -d -m 0755 /etc/modules-load.d
printf 'uinput\n' > /etc/modules-load.d/otd-touchpad.conf
udevadm control --reload-rules
udevadm trigger --action=add --subsystem-match=input --property-match=ID_INPUT_TOUCHPAD=1
udevadm trigger --action=add --subsystem-match=misc --sysname-match=uinput
udevadm settle
printf '\nSetup complete. Touchpad and uinput access is granted to the active local desktop session.\nRun Start OTD Touchpad.sh as your normal user.\n'
