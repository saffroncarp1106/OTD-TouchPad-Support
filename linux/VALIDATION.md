# Linux v0.0.2 validation — 2026-10-23

## Diagnosis

The v0.0.1 Downloads launch log repeatedly reports `linux/OpenTabletDriver.UX.Gtk: No such file or directory`. Both downloaded trees contain source; neither contains the published GUI executable. The machine had no .NET SDK. This was the immediate startup failure, not evidence of an incompatible touchpad kernel driver.

The previous setup also named Debian-only packages and loaded uinput only for the current boot. v0.0.2 adds Arch/CachyOS dependency guidance, source/build-output detection, native-library checks, an installed-service conflict check, and persistent uinput loading when setup runs.

## Verified on this machine

- CachyOS (Arch based), KDE Wayland, x86_64; GTK 3.24.52, libevdev 1.14.0, ICU 78.3.
- Self-contained GUI, daemon and console publish succeeded with .NET SDK 8.0.426. Inherited Windows/Mac CA2216 warnings remain.
- Encoder/unit checks passed: normalization, dimensions, corner mapping, contact changes, lift, palm/multifinger/incomplete-contact rejection, ownership, native ABI.
- Real kernel integration checks passed: synthetic uinput touchpad discovery, coordinate persistence, mapping, multifinger suppression, enable/disable resync and grab cleanup.
- Release validation checked the launcher paths for unbuilt source, an incomplete portable release, source-to-release forwarding, and missing native libraries. These launcher checks do not have a standalone test script in this repository.
- GUI and its child daemon launched as the normal desktop user. ASUS ASUE1209:00 04F3:319F was detected at /dev/input/event10 with 128.71 × 83.16 mm dimensions.
- Virtual pointer initialized successfully; logs confirm Absolute Mode and exclusive touchpad mapping enabled. The console connected to the daemon and saved defaults.
- Wayland display discovery returned eDP-1 and the virtual desktop, both 2048 × 1152 logical pixels.
- This machine already had the touchpad/uinput ACL rule installed; no new administrator changes were needed for this live launch. Boot persistence is supplied by the updated setup script but has not been reboot-tested.

Actual finger-to-cursor accuracy, physical click/drag feel, and other devices/displays still require hands-on evaluation. No new recorded hardware capture was replayed; the historical v0.0.1 capture result below is retained only as history.

Run `dotnet run --project tests/LinuxTouchpad/LinuxTouchpad.csproj -c Release` for encoder checks. Add `-- --integration` with local seat/uinput access for native tests, or `-- --inventory` for dynamic OTD device configurations.

---

# Linux v0.0.1 validation — 2026-10-14

- Release self-contained linux-x64 GTK, daemon, and console builds passed using .NET SDK 8.0.426. Only inherited Windows/Mac CA2216 analyzer warnings remain.
- Encoder tests passed: axis normalization/clamping, signed minimum, dimensions, corners, Y inversion, tracking-ID changes, lift, multiple contacts, palms, incomplete contacts, output ownership, and native ABI.
- ASUS ASUE1209:00 04F3:319F capture replay passed: 1,469 frames, 1,466 mapped positions, and 3 releases. All mapped positions matched the logger's normalized values. Kernel surface dimensions: 128.71 × 83.16 mm.
- Real kernel uinput/evdev integration tests passed: capability discovery, slot-coordinate persistence, frame encoding, multifinger suspension/resume, disable/re-enable state synchronization, and hardware-grab cleanup after disabling output and disposing the stream. Synthetic tests do not generate pointer movement.
- Linux Mint 22.3/X11 GUI startup and daemon connection passed. Closing the GUI stopped the packaged child daemon.
- A subsequent live launch detected the ASUS touchpad and logged absolute mapping enabled. Actual cursor accuracy, drag/button behavior, and comfort still need user evaluation.
- Wayland, Fedora, Apple trackpads, and other models are not validated. This package is x64; Apple Silicon requires a separate ARM64 build.

Run `dotnet run --project tests/LinuxTouchpad/LinuxTouchpad.csproj -c Release` for encoder checks. Add `-- --integration` for kernel tests (requires touchpad/uinput permissions) or `-- --inventory` for dynamic OTD device configurations. Add `-- /path/to/capture.jsonl` to replay a logger capture.
