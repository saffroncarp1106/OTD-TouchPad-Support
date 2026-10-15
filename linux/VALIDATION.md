# Linux v0.0.1 validation — 2026-10-14

- Release self-contained linux-x64 GTK, daemon, and console builds passed using .NET SDK 8.0.426. Only inherited Windows/Mac CA2216 analyzer warnings remain.
- Encoder tests passed: axis normalization/clamping, signed minimum, dimensions, corners, Y inversion, tracking-ID changes, lift, multiple contacts, palms, incomplete contacts, output ownership, and native ABI.
- ASUS ASUE1209:00 04F3:319F capture replay passed: 1,469 frames, 1,466 mapped positions, and 3 releases. All mapped positions matched the logger's normalized values. Kernel surface dimensions: 128.71 × 83.16 mm.
- Real kernel uinput/evdev integration tests passed: capability discovery, slot-coordinate persistence, frame encoding, multifinger suspension/resume, disable/re-enable state synchronization, and hardware-grab cleanup after disabling output and disposing the stream. Synthetic tests do not generate pointer movement.
- Linux Mint 22.3/X11 GUI startup and daemon connection passed. Closing the GUI stopped the packaged child daemon.
- A subsequent live launch detected the ASUS touchpad and logged absolute mapping enabled. Actual cursor accuracy, drag/button behavior, and comfort still need user evaluation.
- Arm64 WIP.

Run `dotnet run --project tests/LinuxTouchpad/LinuxTouchpad.csproj -c Release` for encoder checks. Add `-- --integration` for kernel tests (requires touchpad/uinput permissions) or `-- --inventory` for dynamic OTD device configurations. Add `-- /path/to/capture.jsonl` to replay a logger capture.
