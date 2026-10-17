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


<img width="1170" height="742" alt="image" src="https://github.com/user-attachments/assets/ee8a0c39-9190-4096-9ee6-a271660599b0" />

<img width="916" height="817" alt="Screenshot 2026-10-17 114225" src="https://github.com/user-attachments/assets/ce861935-d315-43c7-900a-5c6336c9742d" />


# MacOS
Todo


# Linux v0.0.1 — experimental prerelease

Todo
