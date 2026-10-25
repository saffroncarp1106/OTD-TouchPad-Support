#!/usr/bin/env bash
# Shell-only regression checks; does not compile the Linux binaries.
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
scratch=$(mktemp -d)
trap 'rm -rf -- "$scratch"' EXIT
fixture="$scratch/source with spaces"
mkdir -p "$fixture"
cp "$repo/build-linux.sh" "$fixture/"
cp -R "$repo/linux" "$fixture/"
for project in OpenTabletDriver.UX.Gtk OpenTabletDriver.Daemon OpenTabletDriver.Console; do
    mkdir -p "$fixture/upstream/$project"
    cp "$repo/upstream/$project/$project.csproj" "$fixture/upstream/$project/"
done
cp "$repo/upstream/LICENSE" "$fixture/upstream/"
cd "$scratch"
bash "$repo/build-linux.sh" --check-source
bash "$fixture/build-linux.sh" --check-source
bash "$fixture/linux/build-linux.sh" --check-source
mv "$fixture/upstream" "$fixture/source-hidden"
if bash "$fixture/build-linux.sh" --check-source > "$scratch/error.log" 2>&1; then
    printf 'FAIL: incomplete source accepted\n' >&2
    exit 1
fi
grep -q 'Missing source project:' "$scratch/error.log"
grep -q 'Resolved source root:' "$scratch/error.log"
[[ ! -e "$fixture/artifacts" ]]
cp "$repo/linux/build-linux.sh" "$scratch/moved-build.sh"
if bash "$scratch/moved-build.sh" --check-source > "$scratch/error.log" 2>&1; then
    printf 'FAIL: relocated script accepted\n' >&2
    exit 1
fi
grep -q 'Incomplete or misplaced source tree' "$scratch/error.log"
printf 'PASS: normal layout, spaces, unrelated working directory, missing source, relocated script\n'
