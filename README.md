# OTD-TouchPad-Support
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


<img width="812" height="226" alt="image" src="https://github.com/user-attachments/assets/1f165429-8183-4cac-b0ed-34966700e9d5" />


<img width="727" height="460" alt="image" src="https://github.com/user-attachments/assets/a1a60b66-49f3-4c62-acf4-b17d19b8f01e" />


<img width="1183" height="901" alt="image" src="https://github.com/user-attachments/assets/4e4c1c3c-508a-4b0a-aafc-a7c7c40b6f01" />


<img width="1472" height="743" alt="image" src="https://github.com/user-attachments/assets/98320a58-e440-4653-8668-9980349434da" />

<img width="1713" height="223" alt="image" src="https://github.com/user-attachments/assets/a9b14143-2d26-4dfb-a386-02735e4f51d7" />

# MacOS
Todo


# Linux v0.0.1 — experimental prerelease

Native Linux touchpad support is available in the [Linux v0.0.1 prerelease](https://github.com/saffroncarp1106/OTD-TouchPad-Support/releases/tag/linux-v0.0.1). Download the self-contained x64 build there. See [Linux setup and supported devices](linux/README.md) and [validation notes](linux/VALIDATION.md).

This version uses kernel absolute touchpad data with OTD Absolute Mode. It supports type B multitouch and single-touch absolute touchpads; support for every model, Wayland desktop, or Apple Silicon is not yet verified.
