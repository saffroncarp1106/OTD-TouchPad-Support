# Build the shared source

Install the .NET 8 SDK and PowerShell 7. Both platform builds use the same `upstream/OpenTabletDriver.sln` tree. Run commands from the repository root. NuGet package restore requires network access on a clean machine.

```powershell
pwsh ./build.ps1 -Platform windows
pwsh ./build.ps1 -Platform macos
```

Windows builds require Windows and produce the self-contained x64 application and diagnostic probe in `artifacts/windows/app/`. Run `OTD-Touchpad-Experimental.exe` there. The macOS target produces self-contained Intel x64 daemon and UI outputs in `artifacts/macos/daemon/` and `artifacts/macos/ui/`; these are unbundled build outputs. For a prepared `.app` with setup instructions, use the published macOS release. Apple Silicon currently requires Rosetta 2.

An explicit SDK executable can be supplied with `-Dotnet /path/to/dotnet`. Generated outputs are excluded from Git. These developer build outputs are not release packages: distribution also needs the appropriate app bundle, source archive and third-party license notices. The original packaging scripts and source are retained in the platform release tags. Published ZIPs were not rebuilt as part of source consolidation.

## Tests

```powershell
dotnet run --project tests/TouchpadQueue/TouchpadQueue.csproj -c Release
dotnet run --project tests/MacTouchpad/MacTouchpad.csproj -c Release
```

The macOS encoder tests can run on Windows and do not exercise Apple's native input callbacks. Real macOS permissions, pointer behavior and Rosetta remain unverified. Optional recorded-capture replay accepts a capture path after `--`; personal captures are not committed.

## Publication validation

On October 13, 2026, both platform targets built successfully from this shared tree on Windows with .NET SDK 8.0.425. The touchpad queue and macOS encoder test suites passed. Existing CA2216 disposal-analysis warnings remain in the native touchpad classes. Cross-compilation does not confirm native macOS runtime behavior.
