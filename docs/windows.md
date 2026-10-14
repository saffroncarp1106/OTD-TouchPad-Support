> Archived usage notes for the published platform release. For the current shared source layout and build commands, see [BUILD.md](../BUILD.md).

# Experimental

Descriptor-driven Windows Precision Touchpad integration for OpenTabletDriver.

## Run

Close other OTD copies and open `Start-OTD-Experimental.cmd`. The application and shareable ZIP are under `release/OTD-Experimental` and `release/`. Closing OTD stops absolute mapping. One finger is mapped at a time.

## Source

- `upstream/OpenTabletDriver/Devices/AsusTouchpad/`: Raw Input transport and decoding.
- `upstream/OpenTabletDriver.Configurations/Parsers/ASUS/`: internal OTD report parser.
- `viewer/`: ASUS box-and-dot diagnostic viewer.
- `src/`: console probe and packaging utilities.
- `tools/NuGet.Config`: package restore configuration.

Builds require the .NET 8 SDK. Python tools require Python 3.9+. Source archives in the release include rebuild instructions. Local SDK/package caches and `build/` are excluded from version control.

Historical patch scripts, reference code, superseded builds, and captures are retained under `.local/`. Installers have been removed. The version's current release is preserved.

OpenTabletDriver and this integration use LGPL-3.0-or-later. License and attribution notices are retained in `upstream/LICENSE` and the release `Licenses/` directory.

## Device selection and testing

A single compatible touchpad is selected automatically. Use `Select-Touchpad.cmd` with OTD closed to select another device, enter its physical dimensions, or change the relative-motion guard. Selection is stored under `%LOCALAPPDATA%\OTD-Touchpad-Experimental`.

The release's `Touchpad-Test/Test-Live-Touchpad.cmd` runs a read-only, single-finger capture. It uses the same decoder as OTD and saves a JSONL file under `Touchpad-Test/results`. Python is required for that launcher; `Touchpad-Probe.exe --list` and `Touchpad-Probe.exe --device 0 --seconds 30` also work directly without Python.

ASUS detection, recorded-report replay, and live single-finger capture have been validated. The Surface descriptor is compatible with the discovery approach, but live Surface input remains unverified. The movement guard may suppress another physical mouse during touchpad use. Firmware smoothing and hardware scan frequency are unchanged.

Repackage with `python src/package-experimental.py` after publishing OTD to `build/experimental` and the console probe to `build/probe`.
