# OpenTabletDriver Touchpad — Linux x64 v0.0.2

This experimental fork turns Linux touchpads into OTD devices for single-finger absolute positioning. It uses a native C# libevdev backend and the kernel's touchpad driver, independently of X11/Wayland. Python and an installed .NET runtime are not needed for the packaged app.

## Supported distributions

The v0.0.2 portable binary is for x64 Linux. It was built and launched on CachyOS (Arch-based) with KDE Wayland. Debian/Ubuntu use is intended, but the v0.0.2 bundle has not been live-tested on those distributions; Linux Mint 22.3/X11 was tested with v0.0.1.

### Arch Linux / CachyOS

v0.0.2 has been built and launched on CachyOS with KDE Wayland and an ASUS ASUE1209 touchpad. The portable release bundles .NET 8; no system .NET installation is needed to run it. Native desktop libraries are still required:

```bash
sudo pacman -S --needed libevdev gtk3 icu libusb
```

Arch package reference: https://archlinux.org/packages/extra/x86_64/gtk3/
.NET SDK reference: https://archlinux.org/packages/extra/x86_64/dotnet-sdk-8.0/

### Binary release versus source download

Run `bash linux/build-linux.sh` from the repository root to create the portable release folder and `artifacts/OTD.v0.0.2.Linux.tar.gz`. Extract the archive to run the release. Its launcher and `OpenTabletDriver.UX.Gtk` executable are in the same directory.

The repository source contains no prebuilt executable. To build it from the repository root:

```bash
sudo pacman -S --needed dotnet-sdk-8.0
bash linux/build-linux.sh
bash "artifacts/OTD-Touchpad-Linux-x64-v0.0.2/Start OTD Touchpad.sh"
```

The source-tree launcher forwards to this build output and builds it when an SDK is available. If an SDK is missing, it explains how to build or obtain the binary release before requesting administrator access. Builds use a new output directory and stop if it already exists, which keeps prior settings and logs out of a fresh release. `OTD_DOTNET` selects an SDK executable; `OTD_LINUX_OUTPUT` selects a custom build/launch output directory. Use a new absolute path for this override.

### Debian / Ubuntu

Install `libevdev2`, `libgtk-3-0`, `libusb-1.0-0`, and your distribution's ICU runtime package. The standard desktop udev and logind services are also required.

The release is v0.0.2; the upstream OTD assembly version remains `0.6.7-touchpad-linux.2` to preserve upstream versioning.

## Start

1. Close other OTD apps and daemons. If you use an installed user service, stop it with `systemctl --user stop opentabletdriver.service` for this session.
2. Install the native libraries for your distribution, then run `bash "Prepare Linux.sh"` once. It asks for your password to load uinput and install `70-otd-touchpad.rules`. Setup also writes `/etc/modules-load.d/otd-touchpad.conf` to load uinput at boot. The rule grants the active local desktop user access to touchpad input nodes and uinput; it does not grant access to keyboards.
3. Run `bash "Start OTD Touchpad.sh"` as your normal desktop user. Never run the GUI with sudo. The launcher also runs setup if uinput permissions are missing.
4. Select your touchpad, whose name ends in `(Absolute)`. The default touchpad output mode is **Absolute Mode**. Choose your display and tablet area, then save settings. New settings live in `userdata` beside this app, separate from existing OTD settings.
5. Test one finger across the four corners. Physical touchpad clicks are forwarded. Lifting releases the contact; multiple fingers or a kernel-reported palm suspend position mapping.

Closing the app stops its worker and releases the touchpad. Disabling output or selecting a different output mode releases the exclusive grab and restores normal touchpad behavior. A crash/process termination also releases the kernel grab. A separate physical mouse remains available. Ordinary gestures, tap-to-click, scrolling, and desktop relative touchpad movement are suspended while absolute mode owns the touchpad; this release preserves physical buttons, not the desktop gesture recognizer.

## Support and settings

The backend discovers devices by capabilities rather than hardcoded vendor/model IDs. It supports type B multitouch touchpads and single-touch absolute touchpads reporting BTN_TOUCH and BTN_TOOL_FINGER. USB, Bluetooth, I2C, and PS/2 devices can work when their Linux kernel driver exposes those capabilities. Legacy type A multitouch, relative-only devices, and drivers without touchpad/contact classification are not supported in this release. This is broad capability-based support, not a guarantee that every touchpad model or compositor works.

Coordinates normalize each axis's kernel minimum/maximum to 0..65535. Linux Y is already oriented toward the bottom; it is not inverted by default. Physical dimensions come from kernel axis resolution. Set these optional environment variables before launching if needed:

```bash
# Select one device by its current event path, or full stable identity from diagnostics.
OTD_TOUCHPAD_DEVICE=/dev/input/event5 bash "Start OTD Touchpad.sh"
# Override both physical dimensions in millimeters.
OTD_TOUCHPAD_WIDTH_MM=128.7 OTD_TOUCHPAD_HEIGHT_MM=83.2 bash "Start OTD Touchpad.sh"
# Reverse Y for an unusual device orientation.
OTD_TOUCHPAD_FLIP_Y=1 bash "Start OTD Touchpad.sh"
```

Device event numbers can change; saved OTD profiles use a stable device identity derived from name, physical location, unique identifier, and bus/vendor/product. Hotplug discovery runs every two seconds. Absolute hardware grabs are enabled only when OTD's Absolute Mode has successfully opened its uinput pointer. Other modes leave the touchpad to the desktop.

## Diagnostics

The app's Console tab reports discovery, permission failures, input errors, and mapping state. Launcher output is also saved to `logs/app.log`; daemon logs are under `userdata/Logs`. If no touchpad appears, rerun setup and Detect Tablets. If necessary log out and back in so logind applies seat access. An SSH/headless session may not receive local-seat ACLs. Display discovery and virtual pointer behavior inherit OTD's compositor support; verify your intended desktop session and monitor arrangement.

The development logger `linux/live_touchpad.py` is separate. Its captures are kernel-decoded input, not raw HID packets. The runtime backend uses libevdev state synchronization for dropped event buffers and prevents a partially updated multitouch frame from being mapped.

## Build and validation

Source is available in this repository. Linux additions are in `upstream/OpenTabletDriver/Devices/LinuxTouchpad`; detection wiring is in `Driver.cs`, output ownership in `OpenTabletDriver.Desktop/Output/AbsoluteMode.cs`, and Linux app worker lifetime in `OpenTabletDriver.UX/DaemonWatchdog.cs` and `App.cs`.

From the repository root, `bash linux/build-linux.sh` publishes self-contained linux-x64 GTK, daemon, and console apps and creates the portable archive using an installed .NET 8 SDK. Set `OTD_DOTNET` to a specific SDK executable if needed; `OTD_LINUX_OUTPUT` and `OTD_LINUX_ARCHIVE` select output paths.

Tests are in `tests/LinuxTouchpad`. They verify normalization, dimensions, corner mapping, contact-ID changes, lift, multi-contact/palm filtering, incomplete contacts, ownership lifetime, and native ABI. Pass the supplied capture path to replay real raw positions against their independently logged normalized values. `--integration` tests real kernel uinput/evdev with a synthetic touchpad, without generating pointer movement. `--inventory` prints dynamic OTD touchpad configurations.

The user's ASUS ASUE1209 capture replay passed: 1,469 frames, 1,466 mapped positions, and 3 releases. It reports a 3990 × 2578 raw range and a 128.71 × 83.16 mm surface. That capture contains only single-finger input; multifinger behavior is covered separately by tests. See `VALIDATION.md` for completed build/native/live checks and remaining limits.

## Remove setup

To remove this fork's permission rule, delete `/etc/udev/rules.d/70-otd-touchpad.rules` and `/etc/modules-load.d/otd-touchpad.conf` as administrator, reload udev rules, and log out/reboot. This app does not blacklist touchpad modules, replace kernel drivers, or install itself as a service. Existing OTD installation is separate.

OpenTabletDriver's LGPL license and bundled dependency notices remain applicable. See `LICENSE`, `Licenses`, and the source. Protocol references: [Linux input event codes](https://docs.kernel.org/input/event-codes.html), [libevdev](https://www.freedesktop.org/software/libevdev/doc/latest/).
