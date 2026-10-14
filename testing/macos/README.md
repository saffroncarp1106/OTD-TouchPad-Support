# macOS touchpad inventory test

Copy `touchpad_info_macos.py` to the Mac. Requires Python 3.9 or newer; no pip packages or compiler are needed.

Run in Terminal from its folder:

```sh
python3 touchpad_info_macos.py --all
```

The script prints device names and identifiers and saves all discovered devices to `results/macos-....json`. Send that JSON back for analysis. Optional arguments: `--match trackpad` and `--output surface-test.json` (choose any filename).

It uses the public IOKit/CoreFoundation APIs to inspect devices and HID elements. It does not open devices for exclusive access, install drivers, change settings, capture live input, or move the pointer. No administrator privileges are requested. Registry identifiers are macOS identifiers, not Windows device paths; element cookies describe the collection hierarchy instead of Windows link-collection numbers.

Standard touchpad collections and trackpad-like names are highlighted as candidates. An Apple trackpad may expose vendor-defined elements, and a successful inventory does not establish access to absolute finger coordinates. Keep the full report even if no candidate is highlighted. A device absent from the public HID inventory will need separate investigation.

Syntax and argument handling were checked on Windows. Native enumeration and the reported fields still need validation on an actual Mac.
