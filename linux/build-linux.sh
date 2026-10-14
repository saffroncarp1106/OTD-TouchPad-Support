#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo=$(cd -- "$folder/.." && pwd)
dotnet_path=${OTD_DOTNET:-dotnet}
command -v "$dotnet_path" >/dev/null || { printf 'Install .NET SDK 8 or set OTD_DOTNET to its executable.\n' >&2; exit 1; }
export DOTNET_CLI_HOME="$repo/.build-home"
export NUGET_PACKAGES="$repo/.packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
output=${OTD_LINUX_OUTPUT:-"$repo/artifacts/OTD-Touchpad-Linux-x64-v0.0.1"}
mkdir -p "$output"
for project in OpenTabletDriver.UX.Gtk OpenTabletDriver.Daemon OpenTabletDriver.Console; do
    "$dotnet_path" publish "$repo/upstream/$project/$project.csproj" -c Release -r linux-x64 --self-contained true -o "$output" -p:VersionSuffix=-touchpad-linux.1
done
cp "$folder/70-otd-touchpad.rules" "$folder/Prepare Linux.sh" "$folder/Start OTD Touchpad.sh" "$folder/README.md" "$folder/VALIDATION.md" "$output/"
cp "$repo/upstream/LICENSE" "$output/LICENSE"
chmod +x "$output/Prepare Linux.sh" "$output/Start OTD Touchpad.sh"
mkdir -p "$output/userdata"
if command -v git >/dev/null && git -C "$repo" diff --quiet && git -C "$repo" diff --cached --quiet; then
    git -C "$repo" archive --format=zip HEAD > "$output/Source.zip"
fi
printf '\nLinux build ready at %s\n' "$output"
