> Archived usage notes for the published platform release. For the current shared source layout and build commands, see [BUILD.md](../BUILD.md).

# Apple touchpad OTD test build

This merges the Windows experimental OTD source with a direct C# MultitouchSupport backend. Python is not needed to run the app. The existing Windows project outside this MacOs folder is unchanged.

## Run on a Mac

1. Extract the release ZIP on the Mac. This is an Intel x64 app; Apple Silicon requires Rosetta 2. It includes its .NET runtime.
2. Close other OTD instances, including their driver workers.
3. Open Terminal in the extracted folder and run `bash Prepare-Mac.command` once. This sets executable permissions and applies a local ad-hoc signature to the custom app. The build is not Apple-notarized.
4. Open `OpenTabletDriver-Touchpad.app`. If macOS blocks it, use the app-specific Open Anyway option in Privacy & Security.
5. Grant the app Accessibility and Input Monitoring permissions when prompted, then reopen it.
6. Select `Apple Built-in Trackpad (Absolute)`, use Absolute Mode, set the display/tablet area, and save settings. If needed, use Detect Tablets.

Use one finger. Multiple active contacts suspend mapping; lifting releases the contact. Closing the OTD window or quitting stops its worker. The mouse filter exists only in the worker process and does not change system driver settings.

## Current scope and things to verify

The first built-in Apple multitouch device is selected. External Magic Trackpads and hot-plug selection are not implemented in this first build. The native ABI matches the 96-byte layout used by the working Python capture. This build has compiled on Windows and replayed the user's capture, but has not been run on a Mac; Rosetta, permissions, native callbacks, and actual pointer behavior require that test.

Coordinates are clamped and scaled from 0..1 to 0..65535. Y is inverted to match a top-left screen origin. If movement is vertically reversed, quit the app and launch its executable from Terminal with `OTD_TOUCHPAD_FLIP_Y=0` set. Surface dimensions use the private framework's reported dimensions divided by 100, with a logged 100 x 100 fallback. Check the aspect ratio against the physical trackpad; these private API units are not an Apple guarantee.

The event filter suppresses hardware mouse movement only while a trackpad contact and recent OTD absolute output are present. It lets generated OTD events through. macOS event taps do not reliably distinguish a physical mouse from trackpad mouse events here, so a separate mouse may briefly be affected while touching the trackpad. Buttons and scrolling are not filtered. Test dragging, lifting, and quitting before judging smoothing or latency.

## Source and validation

Source: `Experimental (v0.1.0)/upstream/OpenTabletDriver/Devices/AsusTouchpad/MacTouchpad.cs`, with detection wiring in `Driver.cs` and parent-process tracking in `OpenTabletDriver.UX/DaemonWatchdog.cs`.

The callback layout follows the approach demonstrated by https://kivy.org/doc/stable/_modules/kivy/input/providers/mactouch.html. MultitouchSupport is a private Apple framework and may change between OS versions. Existing OTD licenses remain applicable.

Tests: `Experimental (v0.1.0)/tests/MacTouchpad`. The supplied capture replay yielded 574 position reports and 3 releases from 591 frames. Tests cover ABI offsets, corner mapping, Y inversion, lift, ID changes, multiple contacts, and non-finite coordinates.
