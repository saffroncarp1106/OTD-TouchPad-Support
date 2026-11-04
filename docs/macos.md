# Apple touchpad on macOS

The current source uses OpenTabletDriver v0.6.8.1 and the existing C# MultitouchSupport backend. Native Apple Silicon (`arm64`) and Intel (`x64`) builds use the same source and produce separate apps. Python is not required.

## Build

From the repository root, with .NET 10 and PowerShell 7:

```powershell
pwsh ./build.ps1 -Platform macos -Architecture arm64
```

Use `-Architecture x64` for Intel. The app and ZIP are in `artifacts/macos-arm64/` or `artifacts/macos-x64/`. The build creates and verifies a local ad-hoc signature; no Prepare-Mac.command step is needed. The app is not notarized.

## Test

1. Quit other OpenTabletDriver instances and their driver workers.
2. Open `OpenTabletDriver-Touchpad.app`. If macOS blocks it, use the app-specific Open Anyway action in Privacy & Security.
3. Grant the app Accessibility and Input Monitoring permissions, then reopen it. A replacement build may require permission to be granted again.
4. Select `Apple Built-in Trackpad (Absolute)`, use Absolute Mode, set the display/tablet area, and save settings. Use Detect Tablets if needed.
5. Test one-finger movement, lifting, a second finger, quitting, and sleep/wake. Confirm ordinary relative movement returns when the app quits.

The backend selects the first built-in Apple trackpad. External Magic Trackpads and hot-plug selection remain outside its current scope. Multiple contacts suspend mapping; lifting releases the contact. Hardware mouse movement can also be suppressed while touching the trackpad because the event tap cannot reliably distinguish the two devices. Buttons and scrolling are not filtered.

Coordinates are clamped to 0..1, scaled to 0..65535, and Y is inverted. Set `OTD_TOUCHPAD_FLIP_Y=0` when launching from Terminal if necessary. Surface dimensions use the framework's reported dimensions divided by 100, with a logged 100 x 100 fallback. Verify the mapping aspect ratio on the actual device.

The 96-byte native contact layout and encoder have automated checks. Those checks and a successful ARM64 build do not establish actual pointer behavior. MultitouchSupport is a private framework and can change between macOS versions. Native ARM64 application support does not itself change the trackpad's reporting rate.

## Release

[macOS V0.0.2](https://github.com/saffroncarp1106/OTD-TouchPad-Support/releases/tag/v0.0.2Mac) provides separate Apple Silicon and Intel downloads. Basic input has been tested on Apple Silicon. The older v0.0.1Mac package remains available; its Prepare-Mac.command applies only to that older package.