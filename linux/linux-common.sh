#!/usr/bin/env bash
# Shared distro-aware diagnostics for the source tree and portable release.
otd_dependency_hint() {
    local ID= ID_LIKE=
    if [[ -r /etc/os-release ]]; then
        # shellcheck disable=SC1091
        source /etc/os-release
    fi

    case " $ID $ID_LIKE " in
        *arch*|*cachyos*)
            printf 'Arch/CachyOS runtime dependencies: sudo pacman -S --needed libevdev gtk3 icu libusb\n'
            printf 'For building source: sudo pacman -S --needed dotnet-sdk-8.0\n'
            ;;
        *debian*|*ubuntu*)
            printf 'Debian/Ubuntu runtime dependencies: libevdev2, libgtk-3-0, libusb-1.0-0, and your distribution ICU package.\n'
            printf 'For building source: install the .NET 8 SDK.\n'
            ;;
        *)
            printf 'Install libevdev, GTK 3, ICU and libusb; source builds require the .NET 8 SDK.\n'
            ;;
    esac
}

otd_check_libraries() {
    local cache library missing=0
    if ! cache=$(ldconfig -p 2>/dev/null); then
        printf 'Cannot inspect native libraries: ldconfig is unavailable.\n' >&2
        return 1
    fi

    for library in libevdev.so.2 libgtk-3.so.0 libicuuc.so libusb-1.0.so.0; do
        if ! grep -F "$library" <<< "$cache" >/dev/null; then
            printf 'Missing native dependency: %s\n' "$library" >&2
            missing=1
        fi
    done

    if (( missing )); then
        otd_dependency_hint >&2
        return 1
    fi
}
