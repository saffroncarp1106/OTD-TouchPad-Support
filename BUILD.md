# Rebuild macOS x64

Install the .NET 8 SDK and PowerShell, then run `pwsh macos/build-macos.ps1`. Outputs are in `build/daemon` and `build/ui`. To package, extract the official OpenTabletDriver 0.6.7 osx-x64 distribution into `OpenTabletDriver-0.6.7_osx-x64/` so it contains `OpenTabletDriver.app`, then run `python macos/package_macos.py` with Python 3.9+. This preserves the original packaging layout.

The release label is macOS v0.0.1; the inherited app bundle version is 0.1.0. The supplied binary was built before publication; the build-script SDK lookup was made portable for this source branch. No runtime behavior was changed. Read PLATFORM.md for setup and testing limitations.
