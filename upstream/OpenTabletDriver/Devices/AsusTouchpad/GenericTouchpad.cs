#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AsusTouchpad;
public record HidField(byte Report, ushort Page, ushort Usage, ushort Link, bool Button, bool Absolute, int Min, int Max, int PhysicalMin, int PhysicalMax, uint Units, int Exponent, int Bits);
public record TouchFrame(List<TouchPoint> Points, uint ContactCount, uint? Scan);
public sealed partial class TouchpadDevice
{
    public uint Vendor, Product;
    public string Name = "Precision Touchpad", Signature = "";
    public int InputLength;
    public readonly List<HidField> Fields = new();
    public readonly ContactFrames Frames = new();
    public float? Width => Millimeters(Axes.FirstOrDefault(a => a.Usage == 0x30));
    public float? Height => Millimeters(Axes.FirstOrDefault(a => a.Usage == 0x31));

    static float? Millimeters(Axis? axis)
    {
        if (axis == null || axis.PhysicalMax <= axis.PhysicalMin)
            return null;
        int exponent = axis.Exponent & 15;
        if (exponent >= 8)
            exponent -= 16;
        double factor = axis.Units == 0x11 ? 10 : axis.Units == 0x13 ? 25.4 : 0;
        double value = ((double)axis.PhysicalMax - axis.PhysicalMin) * Math.Pow(10, exponent) * factor;
        return value > 1 && value < 1000 ? (float)value : null;
    }
}

public sealed class ContactFrames
{
    uint expected;
    uint? scan;
    long started;
    readonly List<(TouchPoint Point, bool Active)> pending = new();
    public TouchFrame? Push(uint count, uint? timestamp, List<(TouchPoint Point, bool Active)> slots)
    {
        if (count > 127)
        {
            pending.Clear();
            expected = 0;
            return new(new(), 0, timestamp);
        }

        if (count > 0)
        {
            pending.Clear();
            expected = count;
            scan = timestamp;
            started = Environment.TickCount64;
        }
        else if (expected == 0)
            return new(new(), 0, timestamp);
        else if (Environment.TickCount64 - started > 100 || (scan.HasValue && timestamp.HasValue && scan != timestamp))
        {
            pending.Clear();
            expected = 0;
            return new(new(), 0, timestamp);
        }

        pending.AddRange(slots.Take((int)expected - pending.Count));
        if (pending.Count < expected)
            return null;
        var points = pending.Where(p => p.Active).Select(p => p.Point).ToList();
        uint total = expected;
        expected = 0;
        pending.Clear();
        if (points.Select(p => p.Id).Distinct().Count() != points.Count)
            return new(new(), 0, timestamp);
        return new(points, total, timestamp);
    }
}

public static partial class TouchpadNative
{
    [DllImport("hid.dll")]
    static extern int HidP_GetButtonCaps(int type, IntPtr caps, ref ushort count, IntPtr data);
    public static List<TouchpadDevice> Enumerate()
    {
        if (!OperatingSystem.IsWindows())
            return new();
        var result = new List<TouchpadDevice>();
        uint count = 0;
        if (GetRawInputDeviceList(null, ref count, (uint)Marshal.SizeOf<RawDevice>()) == uint.MaxValue)
            throw new System.ComponentModel.Win32Exception();
        var list = new RawDevice[count];
        if (GetRawInputDeviceList(list, ref count, (uint)Marshal.SizeOf<RawDevice>()) == uint.MaxValue)
            throw new System.ComponentModel.Win32Exception();
        foreach (var raw in list.Take((int)count))
        {
            if (raw.Type != 2)
                continue;
            IntPtr info = Marshal.AllocHGlobal(64);
            TouchpadDevice? dev = null;
            try
            {
                uint n = 32;
                Marshal.WriteInt32(info, 32);
                if (GetRawInputDeviceInfo(raw.Handle, 0x2000000b, info, ref n) == uint.MaxValue)
                    continue;
                if (Marshal.ReadInt16(info, 20) != 13 || Marshal.ReadInt16(info, 22) != 5)
                    continue;
                dev = new TouchpadDevice
                {
                    Handle = raw.Handle,
                    Vendor = (uint)Marshal.ReadInt32(info, 8),
                    Product = (uint)Marshal.ReadInt32(info, 12)
                };
                n = 0;
                GetRawInputDeviceInfo(raw.Handle, 0x20000007, IntPtr.Zero, ref n);
                IntPtr name = Marshal.AllocHGlobal(checked((int)n * 2 + 2));
                try
                {
                    if (GetRawInputDeviceInfo(raw.Handle, 0x20000007, name, ref n) == uint.MaxValue)
                        throw new System.ComponentModel.Win32Exception();
                    dev.Path = Marshal.PtrToStringUni(name) ?? "";
                }
                finally
                {
                    Marshal.FreeHGlobal(name);
                }

                try
                {
                    var parts = dev.Path.Substring(4).Split('#');
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + string.Join("\\", parts.Take(3)));
                    dev.Name = ((key?.GetValue("FriendlyName") ?? key?.GetValue("DeviceDesc")) as string)?.Split(';').Last() ?? dev.Name;
                }
                catch
                {
                }

                n = 0;
                if (GetRawInputDeviceInfo(raw.Handle, 0x20000005, IntPtr.Zero, ref n) == uint.MaxValue || n == 0)
                    throw new InvalidOperationException("No preparsed data");
                dev.Descriptor = Marshal.AllocHGlobal((int)n);
                if (GetRawInputDeviceInfo(raw.Handle, 0x20000005, dev.Descriptor, ref n) == uint.MaxValue)
                    throw new System.ComponentModel.Win32Exception();
                if (HidP_GetCaps(dev.Descriptor, info) != Success)
                    throw new InvalidOperationException("HID capabilities unavailable");
                dev.InputLength = (ushort)Marshal.ReadInt16(info, 4);
                ReadFields(dev, (ushort)Marshal.ReadInt16(info, 48), false);
                ReadFields(dev, (ushort)Marshal.ReadInt16(info, 46), true);
                foreach (var f in dev.Fields.Where(f => f.Page == 1 && f.Absolute && !f.Button && (f.Usage == 0x30 || f.Usage == 0x31) && f.Max > f.Min))
                    dev.Axes.Add(new(f.Link, f.Usage, f.Min, f.Max, f.PhysicalMin, f.PhysicalMax, f.Units, f.Exponent, f.Report, f.Bits));
                var slots = Slots(dev).ToList();
                if (slots.Count == 0)
                    throw new InvalidOperationException("No complete absolute contact fields");
                dev.Signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dev.InputLength + string.Join("|", dev.Fields))));
                result.Add(dev);
                dev = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Touchpad discovery: " + ex.Message);
            }
            finally
            {
                dev?.Dispose();
                Marshal.FreeHGlobal(info);
            }
        }

        return result;
    }

    static void ReadFields(TouchpadDevice dev, ushort count, bool buttons)
    {
        if (count == 0)
            return;
        IntPtr buffer = Marshal.AllocHGlobal(count * 72);
        try
        {
            int status = buttons ? HidP_GetButtonCaps(0, buffer, ref count, dev.Descriptor) : HidP_GetValueCaps(0, buffer, ref count, dev.Descriptor);
            if (status != Success)
                throw new InvalidOperationException("HID field discovery failed");
            for (int i = 0; i < count; i++)
            {
                IntPtr p = IntPtr.Add(buffer, i * 72);
                ushort lo = (ushort)Marshal.ReadInt16(p, 56), hi = Marshal.ReadByte(p, 12) != 0 ? (ushort)Marshal.ReadInt16(p, 58) : lo;
                ushort page = (ushort)Marshal.ReadInt16(p);
                if (!buttons && (ushort)Marshal.ReadInt16(p, 20) != 1)
                    continue;
                foreach (ushort usage in page == 1 ? new ushort[]
                {
                    0x30,
                    0x31
                }

                : page == 13 ? new ushort[]
                {
                    0x42,
                    0x47,
                    0x51,
                    0x54,
                    0x56
                }

                : Array.Empty<ushort>())
                    if (usage >= lo && usage <= hi)
                        dev.Fields.Add(new(Marshal.ReadByte(p, 2), page, usage, (ushort)Marshal.ReadInt16(p, 6), buttons, Marshal.ReadByte(p, 15) != 0, buttons ? 0 : Marshal.ReadInt32(p, 40), buttons ? 0 : Marshal.ReadInt32(p, 44), buttons ? 0 : Marshal.ReadInt32(p, 48), buttons ? 0 : Marshal.ReadInt32(p, 52), buttons ? 0 : (uint)Marshal.ReadInt32(p, 36), buttons ? 0 : Marshal.ReadInt32(p, 32), buttons ? 1 : (ushort)Marshal.ReadInt16(p, 18)));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    static IEnumerable<TouchpadDevice.Axis> Slots(TouchpadDevice d) => d.Axes.Where(x => x.Usage == 0x30 && d.Axes.Any(y => y.Usage == 0x31 && y.Link == x.Link && y.ReportId == x.ReportId) && d.Fields.Any(f => f.Report == x.ReportId && f.Link == x.Link && f.Page == 13 && f.Usage == 0x51 && !f.Button) && d.Fields.Any(f => f.Report == x.ReportId && f.Link == x.Link && f.Page == 13 && f.Usage == 0x42 && f.Button) && d.Fields.Any(f => f.Report == x.ReportId && f.Page == 13 && f.Usage == 0x54 && !f.Button));
    static uint? Value(TouchpadDevice d, byte[] raw, ushort page, ushort link, ushort usage) => HidP_GetUsageValue(0, page, link, usage, out uint v, d.Descriptor, raw, (uint)raw.Length) == Success ? v : null;
    public static ushort NormalizeAxis(uint raw, TouchpadDevice.Axis axis)
    {
        long value = raw;
        if (axis.Min < 0 && axis.BitSize > 0 && axis.BitSize <= 32)
        {
            long sign = 1L << (axis.BitSize - 1);
            value &= (1L << axis.BitSize) - 1;
            value = (value ^ sign) - sign;
        }

        return (ushort)Math.Round(Math.Clamp(((double)value - axis.Min) / ((double)axis.Max - axis.Min), 0, 1) * 65535);
    }

    public static TouchFrame? Decode(TouchpadDevice d, byte[] raw)
    {
        if (raw.Length == 0)
            return null;
        var slots = Slots(d).Where(x => x.ReportId == raw[0]).ToList();
        if (slots.Count == 0)
            return null;
        var countField = d.Fields.FirstOrDefault(f => f.Report == raw[0] && f.Page == 13 && f.Usage == 0x54 && !f.Button);
        if (countField == null)
            return null;
        var count = Value(d, raw, 13, countField.Link, 0x54);
        if (!count.HasValue)
            return new(new(), 0, null);
        var scanField = d.Fields.FirstOrDefault(f => f.Report == raw[0] && f.Page == 13 && f.Usage == 0x56 && !f.Button);
        var scan = scanField == null ? null : Value(d, raw, 13, scanField.Link, 0x56);
        var contacts = new List<(TouchPoint, bool)>();
        foreach (var x in slots)
        {
            var y = d.Axes.First(a => a.Usage == 0x31 && a.Link == x.Link && a.ReportId == x.ReportId);
            ushort[] buttons = new ushort[128];
            uint n = (uint)buttons.Length;
            if (HidP_GetUsages(0, 13, x.Link, buttons, ref n, d.Descriptor, raw, (uint)raw.Length) != Success)
                return new(new(), 0, scan);
            var vx = Value(d, raw, 1, x.Link, 0x30);
            var vy = Value(d, raw, 1, y.Link, 0x31);
            var id = Value(d, raw, 13, x.Link, 0x51);
            if (!vx.HasValue || !vy.HasValue || !id.HasValue)
                return new(new(), 0, scan);
            bool confidence = d.Fields.Any(f => f.Report == raw[0] && f.Link == x.Link && f.Page == 13 && f.Usage == 0x47 && f.Button);
            bool active = buttons.Take((int)n).Contains((ushort)0x42) && (!confidence || buttons.Take((int)n).Contains((ushort)0x47));
            contacts.Add((new(id.Value, NormalizeAxis(vx.Value, x), NormalizeAxis(vy.Value, y)), active));
        }

        return d.Frames.Push(count.Value, scan, contacts);
    }
}