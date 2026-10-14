#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AsusTouchpad;
public sealed class TouchpadProfile
{
    public string Path { get; set; } = "";
    public string Signature { get; set; } = "";
    public uint Vendor { get; set; }
    public uint Product { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public bool SuppressRelativeMovement { get; set; } = true;
    public bool ValidDimensions => float.IsFinite(Width) && float.IsFinite(Height) && Width > 1 && Height > 1 && Width < 1000 && Height < 1000;
    public static string FilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OTD-Touchpad-Experimental", "touchpad.json");

    public static TouchpadProfile? Load() => File.Exists(FilePath) ? JsonSerializer.Deserialize<TouchpadProfile>(File.ReadAllText(FilePath)) : null;
    public static TouchpadDevice? Resolve(IReadOnlyList<TouchpadDevice> devices, TouchpadProfile? profile)
    {
        if (profile == null)
            return devices.Count == 1 ? devices[0] : null;
        var exact = devices.Where(d => string.Equals(d.Path, profile.Path, StringComparison.OrdinalIgnoreCase) && d.Signature == profile.Signature && d.Vendor == profile.Vendor && d.Product == profile.Product).ToList();
        if (exact.Count == 1)
            return exact[0];
        if (devices.Any(d => string.Equals(d.Path, profile.Path, StringComparison.OrdinalIgnoreCase)))
            return null;
        var fallback = devices.Where(d => d.Vendor == profile.Vendor && d.Product == profile.Product && d.Signature == profile.Signature).ToList();
        return fallback.Count == 1 ? fallback[0] : null;
    }

    public static TouchpadProfile Save(TouchpadDevice device, float? width = null, float? height = null, bool suppress = true)
    {
        var profile = new TouchpadProfile
        {
            Path = device.Path,
            Signature = device.Signature,
            Vendor = device.Vendor,
            Product = device.Product,
            Width = width ?? device.Width ?? 0,
            Height = height ?? device.Height ?? 0,
            SuppressRelativeMovement = suppress
        };
        if (!float.IsFinite(profile.Width) || !float.IsFinite(profile.Height) || profile.Width <= 1 || profile.Height <= 1 || profile.Width >= 1000 || profile.Height >= 1000)
            throw new InvalidOperationException("Touchpad dimensions unavailable. Enter measured width and height in millimeters during setup.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
        return profile;
    }
}