# Build the shared source

The shared `upstream/` tree is based on OpenTabletDriver v0.6.8.1 and requires the .NET 10 SDK. Windows and macOS builds use PowerShell 7; Linux builds use Bash. A clean restore requires network access.

## Windows and macOS

Run from the repository root:

```powershell
pwsh ./build.ps1 -Platform windows -Architecture x64
pwsh ./build.ps1 -Platform windows -Architecture arm64
pwsh ./build.ps1 -Platform macos -Architecture arm64
pwsh ./build.ps1 -Platform macos -Architecture x64
```

Architecture defaults to x64. Use `-Dotnet /path/to/dotnet` when the SDK is not on PATH. Each target has its own `artifacts/<platform>-<architecture>/` output directory. Builds are self-contained, so users do not need .NET installed.

Windows outputs the existing `app/OTD-Touchpad-Experimental.exe`, setup command, and diagnostic tools. macOS builds must run on a Mac; they produce `OpenTabletDriver-Touchpad.app` and a ZIP in the target directory. The Mac bundle uses a local ad-hoc signature, retains the existing touchpad bundle identifier, and requires no preparation script. It is not notarized. See [docs/macos.md](docs/macos.md) for permissions and testing.

## Linux

```bash
bash build-linux.sh
OTD_ARCHITECTURE=arm64 bash build-linux.sh
```

Architecture defaults to x64. Use `OTD_DOTNET=/path/to/dotnet` if needed. Outputs and archives include the OTD version and architecture. Existing GTK 3, libevdev, libusb, ICU, and udev/logind requirements remain applicable; dependencies must match the target architecture. Existing output paths are not overwritten. `--check-source` validates the source layout without building.

The existing GitHub Linux workflow is pinned to the historical V0.0.3Linux release and remains a rebuild workflow for that release. Use these local commands for the updated source. The platform releases are Windows v0.1.3, macOS V0.0.2, and Linux V0.0.4.

## Checks

```bash
dotnet run --project tests/TouchpadQueue/TouchpadQueue.csproj -c Release
dotnet run --project tests/MacTouchpad/MacTouchpad.csproj -c Release
dotnet run --project tests/LinuxTouchpad/LinuxTouchpad.csproj -c Release
bash tests/linux-build-layout.sh
```

The Windows WPF debugger tests run on Windows:

```powershell
dotnet run --project tests/DebuggerLabel/DebuggerLabel.csproj -c Release
```

The encoder/queue tests do not exercise real native touchpad callbacks. Cross-compilation confirms build compatibility, not hardware behavior. Before publishing, test the actual target machines and include the matching source and third-party notices with release packages. Existing upstream/native disposal-analysis warnings remain.

## Local migration validation

The macOS ARM64/Intel, Windows x64/ARM64, and Linux x64/ARM64 builds succeeded locally. The Windows WPF debugger test project compiled; running its UI test still requires Windows. The queue, Mac encoder/ABI, Linux encoder/ABI, and source-layout checks passed.

On macOS, the upstream suite passed 2,135 tests with 7 skips using `--filter 'FullyQualifiedName!~HidSharpEndpointTest'`. That class assumes the process is running on Linux when testing Linux HID paths, so it is excluded on this Mac. The virtual ASUS configuration uses upstream's existing SkipTests metadata for physical LPI checks because its coordinates are normalized; its actual dimensions are preserved. Basic macOS ARM64 touchpad operation has been tested. Windows, Linux, Intel macOS, and extended sleep/wake behavior still require testing on the target hardware.