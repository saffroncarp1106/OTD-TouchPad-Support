# Upstream provenance

Based on https://github.com/OpenTabletDriver/OpenTabletDriver at tag v0.6.8.1, commit f3927ac11146cb7dd5c3b43277d5d1e33e36f6d6. The modified shared source is in `upstream/`; upstream license notices and metadata remain intact.

The prior base was 736003ed72c8bbb28033b039d5a0bb76c344145c (v0.6.7). The v0.6.8.1 update preserves the Windows Raw Input, macOS MultitouchSupport, and Linux evdev touchpad backends, setup UI, report queue, cursor suppression, and UI-owned daemon lifecycle. The virtual touchpad endpoints implement the new device initialization methods as unsupported because they expose input only.

The experimental release tags preserve the source snapshots for the original published binaries. Development continues in the existing TouchPad-OTD branch.