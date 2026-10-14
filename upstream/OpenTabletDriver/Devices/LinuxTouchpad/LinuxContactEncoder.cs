using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTabletDriver.Devices.LinuxTouchpad;

internal readonly record struct LinuxAxis(int Min, int Max, int Resolution)
{
    internal ushort Normalize(int value) => (ushort)Math.Round(Math.Clamp(
        ((double)value - Min) / ((double)Max - Min), 0, 1) * 65535);
    internal bool Valid => Max > Min;
    internal float? Millimeters => Valid && Resolution > 0 ? (float)((double)Max - Min) / Resolution : null;
}

internal readonly record struct LinuxContact(int Id, int X, int Y, bool Complete = true, bool Palm = false);

internal sealed class LinuxContactEncoder
{
    private int? primary;
    internal byte[][] Encode(IReadOnlyList<LinuxContact> contacts, LinuxAxis xAxis, LinuxAxis yAxis, bool flipY = false)
    {
        if (contacts.Count != 1 || !contacts[0].Complete || contacts[0].Palm || !xAxis.Valid || !yAxis.Valid)
            return Release();
        var point = contacts[0];
        ushort x = xAxis.Normalize(point.X), y = yAxis.Normalize(point.Y);
        if (flipY) y = (ushort)(65535 - y);
        var report = new byte[] { 1, 0, (byte)x, (byte)(x >> 8), (byte)y, (byte)(y >> 8), 0, 0, 0 };
        bool changed = primary.HasValue && primary.Value != point.Id;
        primary = point.Id;
        return changed ? new[] { new byte[9], report } : new[] { report };
    }
    internal byte[][] Release()
    {
        bool active = primary.HasValue;
        primary = null;
        return active ? new[] { new byte[9] } : Array.Empty<byte[]>();
    }
}
