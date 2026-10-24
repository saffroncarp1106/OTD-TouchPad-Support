# OTD-TouchPad-Support

# Windows
The goal of the app was to add touchpad support with OTD, to allow for absolute mode and custom sizes. The default windows cursor settings were minimal and.. eh. I have verified it works with the Asus g15, Microsoft Surface, and an old Dell latitude.

To use the program:
On a new laptop, try and use Select-touchpad.cmd,
A menu should appear, if it does not:
1) open the touchpad test folder
2) Scan-touchpads.cmd
    Ensure something appears and a touchpad appears.
3) touchpad-probe.exe
    This should track the positional data the touchpad outputs, this is just a second step to verify its data is exposed to be used for OTD.
4) Try and run the Select-touchpad.cmd again
    If this fails, send me a DM, and with the .json files it should output.
After this, in Select-touchpad.cmd, make sure the dimensions of the touchpad are correct, you can verify this with a google search, it should be fine, I've tried 3 laptops and all were more or less correct.

Lastly, Open the OTD-Touchpad-Experimental.exe
Enjoy, hopefully.
There are quirks, it only supports 1 finger input at a time, it will just and skip if you do this, i recommend palm rejection techniques like gloves. 


<img width="936" height="594" alt="image" src="https://github.com/user-attachments/assets/ee8a0c39-9190-4096-9ee6-a271660599b0" />

<img width="916" height="817" alt="Screenshot 2026-10-17 114225" src="https://github.com/user-attachments/assets/ce861935-d315-43c7-900a-5c6336c9742d" />


# MacOS
Todo


# Linux v0.0.2 — experimental

The Linux x64 release is intended for Arch-based and Debian/Ubuntu-based distributions. It bundles .NET 8; GTK 3, libevdev, ICU, libusb, and the normal desktop udev/logind environment must be installed by the user. It has been tested with Linux Mint, CachyOS.
To Do: Fedora handeling + Macbook linux distro

To run, extract the Linux x64 release, install the native libraries for your distribution, and run <code>Prepare Linux.sh</code> once. Then run <code>Start OTD Touchpad.sh</code> as your normal desktop user.

Debian

<code>sudo apt-get update</code>

<code>sudo apt-get install libgtk-3-dev libevdev-dev libicu-dev libusb-1.0-0-dev</code>

<code>tar -xzf OTD-Touchpad-Linux-x64.tar.gz</code>

<code>cd OTD-Touchpad-Linux-x64</code>

<code>./Prepare\ Linux.sh</code>

<code>./Start\ OTD\ Touchpad.sh</code>


Arch

<code>sudo pacman -Sy</code>

<code>sudo pacman -S gtk3 evdev icu libusb</code>

<code>tar -xzf OTD-Touchpad-Linux-x64.tar.gz</code>

<code>cd OTD-Touchpad-Linux-x64</code>

<code>./Prepare\ Linux.sh</code>

<code>./Start\ OTD\ Touchpad.sh</code>


See [linux/README.md](linux/README.md) for dependencies, setup details, supported touchpad capabilities, build instructions, limitation, and clanker slop.

<img width="960" height="792" alt="image" src="https://github.com/user-attachments/assets/176c7a45-beb3-41b0-8425-e5877147ec57" />


# Debugger
The debugger software is now very in-depth now,
It provides the HID device string and TouchPad info, a Probe script for positional data, as well as a OTD DeviceReport to verify if the probe info is reaching OTD Dameon.

To run, ensure all files are in the same location.
- Execute: <code>Python touchpad_debug.py</code>
- Open <code>OTD-Touchpad-Experimental.exe</code>
- After using the Touchpad, click <code>Ctrl+C</code>
There should be a .json file created in the <code>Results</code> folder.
Send that to a dev if you are having issues.

<img width="1701" height="717" alt="image" src="https://github.com/user-attachments/assets/8f30ef33-e386-4e5f-b357-63e381977b89" />

