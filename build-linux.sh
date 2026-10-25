#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
if [[ ! -f "$repo/linux/build-linux.sh" ]]; then
    printf 'Missing linux/build-linux.sh. Extract the complete source ZIP and keep its folder structure intact.\n' >&2
    exit 1
fi
exec bash "$repo/linux/build-linux.sh" "$@"
