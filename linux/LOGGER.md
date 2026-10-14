# Linux touchpad positional logger

`live_touchpad.py` is the first diagnostic step for the Linux OTD touchpad backend. It reads kernel-decoded absolute events from `/dev/input/event*`, before desktop pointer acceleration, using Python 3 and `evdev`. It never grabs the device, moves the pointer, or changes driver settings. Normal touchpad behavior remains active.

The reference implementations are Windows `OTD-Experimental/Touchpad-Test/live_touchpad.py` and its C# TouchpadProbe, plus `macos/touchpad_macinfo.py` and the multitouch backend inside the Mac `Source.zip`. The Linux OTD distribution in `OTD Linux download` is the base for later integration; this diagnostic runs independently of OTD.

## Run

Install your distribution's `python3-evdev` package (for Debian/Ubuntu: `sudo apt install python3-evdev`). Alternatively install `evdev` inside a Python virtual environment and use that environment's Python.

From this folder:

```bash
python3 live_touchpad.py --list
python3 live_touchpad.py --device /dev/input/event10 --seconds 30
```

Replace `event10` with the touchpad path from the inventory. If access is denied, use `sudo python3 live_touchpad.py --list` and `sudo python3 live_touchpad.py --device /dev/input/event10 --seconds 30`. When using a virtual environment, give sudo the full path to its Python executable. Avoid changing input-device permissions globally.

Automatic selection requires exactly one accessible touchpad candidate. Explicit selection also allows testing other absolute input devices. Event node numbers can change after reconnecting or rebooting; inventory again when needed.

Move one finger across all four corners and edges, lift and repeat, then try two and three fingers. Ctrl+C ends capture early. Logs are saved beside the script in `results/live-<timestamp>.jsonl`. Use `--output new-file.jsonl` to choose a new file; existing files are never overwritten. `--list --output inventory.json` saves the full inventory.

## Capture format and scope

- `start`: device identity, input properties, supported keys, axis minimum/maximum/resolution, protocol, and estimated dimensions in mm when the kernel supplies resolution.
- `frame`: emitted at `SYN_REPORT`, with elapsed monotonic `seconds`, kernel `event_seconds`, frame number `scan`, contact `count`, `buttons` (active key codes), and `points`.
- Each point has Windows-compatible `Id`, `X`, `Y` (0..65535), `raw_x`, `raw_y`, `slot`, and `raw_axes` keyed by Linux event code. Normalization uses the kernel axis range; Y is not inverted. `complete=false` means one or more active contacts lack full coordinates; do not map such a frame to OTD.
- `dropped` / `resync`: an event-buffer overrun occurred; affected frames were discarded and state queried again. These are capture discontinuities, not finger releases.
- `end`: frame count, overruns, elapsed time, and stop reason. Device removal/read failure produces an `error`, an `end`, and a nonzero exit status.

Supports modern type B multitouch slots and single-touch absolute devices with `BTN_TOUCH`. Legacy type A devices are identified but rejected by this first decoder. Extra contact axes, when delivered, are preserved for investigating pressure, size, and palm classification. Contacts are not filtered for palms or mapped to tablet reports yet. Single-touch hardware may report additional fingers through `BTN_TOOL_*` keys without independent positions; inspect `buttons` before building single-finger mapping.

This requires a Linux kernel driver exposing absolute input positions. A device exposing only relative mouse motion cannot provide absolute positions through this logger. It works independently of X11/Wayland and does not read raw HID packets directly. Linux event framing follows the [kernel multitouch protocol](https://kernel.org/doc/html/v5.19/input/multi-touch-protocol.html); device access uses [python-evdev](https://python-evdev.readthedocs.io/en/stable/apidoc.html).

Validation: Python compilation and synthetic decoder checks cover axis endpoints, slot delta persistence, multiple contacts, release, tracking-ID replacement, incomplete contacts, and single-touch lift. Live hardware validation is pending because the development environment exposes no `/dev/input` and has no `evdev` installed.
