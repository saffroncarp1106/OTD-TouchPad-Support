#!/usr/bin/env bash
set -euo pipefail
folder=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo=$(cd -- "$folder/.." && pwd)
projects=(OpenTabletDriver.UX.Gtk OpenTabletDriver.Daemon OpenTabletDriver.Console)
# Check the archive layout before creating output or invoking MSBuild.
missing=0
for project in "${projects[@]}"; do
    project_path="$repo/upstream/$project/$project.csproj"
    if [[ ! -f "$project_path" ]]; then
        printf 'Missing source project: %s\n' "$project_path" >&2
        missing=1
    fi
done
for resource in linux-common.sh 'Prepare Linux.sh' 'Start OTD Touchpad.sh' 70-otd-touchpad.rules README.md VALIDATION.md; do
    if [[ ! -f "$folder/$resource" ]]; then
        printf 'Missing release resource: %s\n' "$folder/$resource" >&2
        missing=1
    fi
done
if [[ ! -f "$repo/upstream/LICENSE" ]]; then
    printf 'Missing source license: %s\n' "$repo/upstream/LICENSE" >&2
    missing=1
fi
if (( missing )); then
    printf '\nIncomplete or misplaced source tree. Extract the entire source ZIP into a new folder.\nKeep linux/ and upstream/ together; do not move build-linux.sh out of linux/.\nRun bash build-linux.sh from the extracted folder containing both directories.\nResolved source root: %s\n' "$repo" >&2
    exit 1
fi
if [[ ${1:-} == --check-source ]]; then
    printf 'Source layout OK: %s\n' "$repo"
    exit 0
elif (( $# )); then
    printf 'Usage: bash build-linux.sh [--check-source]\n' >&2
    exit 1
fi
dotnet_path=${OTD_DOTNET:-dotnet}
command -v "$dotnet_path" >/dev/null || { printf 'Install .NET SDK 8 or set OTD_DOTNET to its executable.\n' >&2; exit 1; }
if ! sdks=$("$dotnet_path" --list-sdks) || [[ -z "$sdks" ]]; then
    printf 'No .NET SDK found. The runtime alone cannot compile this source; install the .NET 8 SDK.\n' >&2
    exit 1
fi
command -v tar >/dev/null || { printf 'Install tar to create the portable release archive.\n' >&2; exit 1; }
export DOTNET_CLI_HOME="$repo/.build-home"
export NUGET_PACKAGES="$repo/.packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
output=${OTD_LINUX_OUTPUT:-"$repo/artifacts/OTD-Touchpad-Linux-x64-v0.0.3"}
archive=${OTD_LINUX_ARCHIVE:-"$repo/artifacts/OTD.v0.0.3.Linux.tar.gz"}
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
for project in "${projects[@]}"; do
    "$dotnet_path" publish "$repo/upstream/$project/$project.csproj" -c Release -r linux-x64 --self-contained true -o "$stage" -p:VersionSuffix=-touchpad-linux.3
done
cp "$folder/70-otd-touchpad.rules" "$folder/linux-common.sh" "$folder/Prepare Linux.sh" "$folder/Start OTD Touchpad.sh" "$folder/README.md" "$folder/VALIDATION.md" "$stage/"
cp "$repo/upstream/LICENSE" "$stage/LICENSE"
chmod +x "$stage/Prepare Linux.sh" "$stage/Start OTD Touchpad.sh"
mkdir -p "$stage/userdata"
if command -v git >/dev/null && [[ -e "$repo/.git" ]] && [[ -z $(git -C "$repo" status --porcelain --untracked-files=all) ]]; then
    git -C "$repo" archive --format=zip HEAD > "$stage/Source.zip"
fi
mv -- "$stage" "$output"
tar -czf "$archive" -C "$(dirname -- "$output")" "$(basename -- "$output")"
printf '\nLinux build ready at %s\nPortable release archive: %s\n' "$output" "$archive"
