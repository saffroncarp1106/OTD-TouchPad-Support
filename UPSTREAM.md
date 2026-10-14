# Upstream provenance

Based on https://github.com/OpenTabletDriver/OpenTabletDriver at commit 736003ed72c8bbb28033b039d5a0bb76c344145c. The modified shared source is in `upstream/`; license notices and upstream metadata remain intact.

The Windows and macOS snapshots shared 1,014 identical source files. The combined tree uses the macOS snapshot: it adds MacTouchpad.cs and platform-aware changes to App.cs, DaemonWatchdog.cs, and Driver.cs. Windows input remains in the same shared tree.

The experimental/windows and experimental/macos branches and release tags preserve the source snapshots for the original published binaries. Development belongs on main.
