#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo=$(cd -- "$folder/.." && pwd)
dotnet_path=${OTD_DOTNET:-dotnet}
command -v "$dotnet_path" >/dev/null || { printf 'Install .NET SDK 8 or set OTD_DOTNET to its executable.\n' >&2; exit 1; }
command -v tar >/dev/null || { printf 'Install tar to create the portable release archive.\n' >&2; exit 1; }
export DOTNET_CLI_HOME="$repo/.build-home"
export NUGET_PACKAGES="$repo/.packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
output=${OTD_LINUX_OUTPUT:-"$repo/artifacts/OTD-Touchpad-Linux-x64-v0.0.2"}
archive=${OTD_LINUX_ARCHIVE:-"$repo/artifacts/OTD.v0.0.2.Linux.tar.gz"}
if [[ -e "$output" ]]; then
    printf 'Build output already exists: %s\nChoose a new OTD_LINUX_OUTPUT directory to avoid carrying old settings or logs into the release.\n' "$output" >&2
    exit 1
fi
if [[ -e "$archive" ]]; then
    printf 'Release archive already exists: %s\nChoose a new OTD_LINUX_ARCHIVE path to avoid overwriting it.\n' "$archive" >&2
    exit 1
fi
mkdir -p "$(dirname -- "$output")"
mkdir -p "$(dirname -- "$archive")"
stage=$(mktemp -d "${output}.build.XXXXXX")
for project in OpenTabletDriver.UX.Gtk OpenTabletDriver.Daemon OpenTabletDriver.Console; do
    "$dotnet_path" publish "$repo/upstream/$project/$project.csproj" -c Release -r linux-x64 --self-contained true -o "$stage" -p:VersionSuffix=-touchpad-linux.2
done
cp "$folder/70-otd-touchpad.rules" "$folder/linux-common.sh" "$folder/Prepare Linux.sh" "$folder/Start OTD Touchpad.sh" "$folder/README.md" "$folder/VALIDATION.md" "$stage/"
cp "$repo/upstream/LICENSE" "$stage/LICENSE"
chmod +x "$stage/Prepare Linux.sh" "$stage/Start OTD Touchpad.sh"
mkdir -p "$stage/userdata"
if command -v git >/dev/null && [[ -z $(git -C "$repo" status --porcelain --untracked-files=all) ]]; then
    git -C "$repo" archive --format=zip HEAD > "$stage/Source.zip"
fi
mv -- "$stage" "$output"
tar -czf "$archive" -C "$(dirname -- "$output")" "$(basename -- "$output")"
printf '\nLinux build ready at %s\nPortable release archive: %s\n' "$output" "$archive"
