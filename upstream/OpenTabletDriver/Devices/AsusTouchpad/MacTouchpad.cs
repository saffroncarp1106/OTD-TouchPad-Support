using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Devices;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Devices.AsusTouchpad;

[DeviceHub, SupportedPlatform(PluginPlatform.MacOS)]
public sealed class MacTouchpadHub : IDeviceHub
{
    public event EventHandler<DevicesChangedEventArgs> DevicesChanged { add { } remove { } }
    public IEnumerable<IDeviceEndpoint> GetDevices() => MacTouchpadSelection.Devices;
}

public static class MacTouchpadSelection
{
    static readonly object Sync = new();
    static MacTouchpadEndpoint[] devices;
    internal static MacTouchpadEndpoint[] Devices
    {
        get
        {
            lock (Sync)
            {
                if (devices != null) return devices;
                var list = MacTouchpadNative.MTDeviceCreateList();
                if (list == IntPtr.Zero) return Array.Empty<MacTouchpadEndpoint>();
                try
                {
                    int count = (int)MacTouchpadNative.CFArrayGetCount(list);
                    for (int i = 0; i < count; i++)
                    {
                        var device = MacTouchpadNative.CFArrayGetValueAtIndex(list, i);
                        if (!MacTouchpadNative.MTDeviceIsBuiltIn(device)) continue;
                        MacTouchpadNative.CFRetain(device);
                        devices = new[] { new MacTouchpadEndpoint(device) };
                        return devices;
                    }
                    Log.Write("Touchpad", "No built-in Apple multitouch trackpad found.");
                    return Array.Empty<MacTouchpadEndpoint>();
                }
                finally { MacTouchpadNative.CFRelease(list); }
            }
        }
    }

    public static IEnumerable<TabletConfiguration> Configurations()
    {
        if (!OperatingSystem.IsMacOS()) return Array.Empty<TabletConfiguration>();
        try
        {
            return Devices.Select(e => new TabletConfiguration
            {
                Name = e.ProductName,
                Attributes = new Dictionary<string, string> { ["PrecisionTouchpad"] = "true" },
                Specifications = new TabletSpecifications
                {
                    Digitizer = new DigitizerSpecifications { Width = e.Width, Height = e.Height, MaxX = 65535, MaxY = 65535 },
                    Pen = new PenSpecifications { MaxPressure = 1, ButtonCount = 0 }
                },
                DigitizerIdentifiers = new List<DeviceIdentifier>
                {
                    new DeviceIdentifier { VendorID = 65534, ProductID = 1210, InputReportLength = 9,
                        ReportParser = "OpenTabletDriver.Configurations.Parsers.ASUS.AsusTouchpadReportParser" }
                }
            }).ToArray();
        }
        catch (Exception ex) { Log.Exception(ex); return Array.Empty<TabletConfiguration>(); }
    }
}

internal sealed class MacTouchpadEndpoint : IDeviceEndpoint
{
    internal readonly IntPtr Device;
    internal readonly float Width, Height;
    internal MacTouchpadEndpoint(IntPtr device)
    {
        Device = device;
        if (MacTouchpadNative.MTDeviceGetSensorSurfaceDimensions(device, out int w, out int h) == 0 && w > 0 && h > 0)
        {
            Width = w / 100f;
            Height = h / 100f;
        }
        else { Width = 100; Height = 100; Log.Write("Touchpad", "Surface dimensions unavailable; using a 100 x 100 mapping area."); }
    }
    public int VendorID => 65534;
    public int ProductID => 1210;
    public int InputReportLength => 9;
    public int OutputReportLength => 0;
    public int FeatureReportLength => 0;
    public string Manufacturer => "Apple";
    public string ProductName => "Apple Built-in Trackpad (Absolute)";
    public string FriendlyName => ProductName;
    public string SerialNumber => "builtin-multitouch";
    public string DevicePath => "multitouch:apple:builtin";
    public bool CanOpen => true;
    public IDictionary<string, string> DeviceAttributes => new Dictionary<string, string> { ["Transport"] = "MultitouchSupport" };
    public string GetDeviceString(byte index) => index == 1 ? Manufacturer : ProductName;
    public IDeviceEndpointStream Open() => new MacTouchpadStream(Device);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MacPoint { public float X, Y; }
[StructLayout(LayoutKind.Sequential)]
internal struct MacVector { public MacPoint Position, Velocity; }
[StructLayout(LayoutKind.Sequential)]
internal struct MacContact
{
    public int Frame;
    public double Timestamp;
    public int Identifier, State, Unknown1, Unknown2;
    public MacVector Normalized;
    public float Size;
    public int Unknown3;
    public float Angle, MajorAxis, MinorAxis;
    public MacVector Unknown4;
    public int Unknown5, Unknown6;
    public float Unknown7;
}

internal sealed class MacContactEncoder
{
    int? primary;
    internal byte[][] Encode(MacContact[] contacts, bool flipY = true)
    {
        var active = contacts.Where(c => c.State == 3 || c.State == 4).ToArray();
        if (active.Length != 1 || !float.IsFinite(active[0].Normalized.Position.X) || !float.IsFinite(active[0].Normalized.Position.Y))
        {
            bool release = primary.HasValue;
            primary = null;
            return release ? new[] { new byte[9] } : Array.Empty<byte[]>();
        }
        var point = active[0];
        ushort x = (ushort)Math.Round(Math.Clamp(point.Normalized.Position.X, 0, 1) * 65535);
        float yValue = Math.Clamp(point.Normalized.Position.Y, 0, 1);
        ushort y = (ushort)Math.Round((flipY ? 1 - yValue : yValue) * 65535);
        var report = new byte[] { 1, 0, (byte)x, (byte)(x >> 8), (byte)y, (byte)(y >> 8), 0, 0, 0 };
        bool changed = primary.HasValue && primary.Value != point.Identifier;
        primary = point.Identifier;
        return changed ? new[] { new byte[9], report } : new[] { report };
    }
}

internal sealed class MacTouchpadStream : IDeviceEndpointStream
{
    readonly LatestTouchpadReports reports = new();
    readonly MacContactEncoder encoder = new();
    readonly IntPtr device;
    readonly Thread thread;
    readonly ManualResetEventSlim ready = new(false);
    readonly MacTouchpadNative.FrameCallback callback;
    readonly MacTouchpadNative.TapCallback tapCallback;
    readonly bool flipY = Environment.GetEnvironmentVariable("OTD_TOUCHPAD_FLIP_Y") != "0";
    volatile bool stopping;
    int disposed;
    Exception startupError;
    IntPtr tap;
    long lastContact;
    public MacTouchpadStream(IntPtr device)
    {
        this.device = device;
        callback = OnFrame;
        tapCallback = OnMouse;
        thread = new Thread(Run) { IsBackground = true, Name = "Apple touchpad" };
        thread.Start();
        ready.Wait();
        if (startupError != null) { Dispose(); throw new InvalidOperationException("Apple touchpad could not start.", startupError); }
    }
    int OnFrame(int sender, IntPtr data, int count, double timestamp, int frame)
    {
        if (stopping) return 0;
        try
        {
            if (count < 0 || count > 32 || (count > 0 && data == IntPtr.Zero)) throw new InvalidOperationException("Unexpected multitouch frame.");
            var points = new MacContact[count];
            for (int i = 0; i < count; i++) points[i] = Marshal.PtrToStructure<MacContact>(data + i * 96);
            lock (encoder)
            {
                var packets = encoder.Encode(points, flipY);
                foreach (var packet in packets)
                {
                    Interlocked.Exchange(ref lastContact, packet[0] == 1 ? Environment.TickCount64 : 0);
                    Interlocked.Exchange(ref global::AsusTouchpad.TouchpadControl.LastContact, packet[0] == 1 ? Environment.TickCount64 : 0);
                    reports.Enqueue(packet);
                }
            }
        }
        catch (Exception ex) { Interlocked.Exchange(ref lastContact, 0); Log.Exception(ex); reports.Dispose(); stopping = true; }
        return 0;
    }
    IntPtr OnMouse(IntPtr proxy, uint type, IntPtr evt, IntPtr context)
    {
        if (type == 0xfffffffe || type == 0xffffffff) { MacTouchpadNative.CGEventTapEnable(tap, true); return evt; }
        if (!stopping && Environment.TickCount64 - Interlocked.Read(ref lastContact) < 100 &&
            global::AsusTouchpad.TouchpadControl.ShouldSuppress && MacTouchpadNative.CGEventGetIntegerValueField(evt, 41) == 0)
            return IntPtr.Zero;
        return evt;
    }
    void Run()
    {
        IntPtr mode = IntPtr.Zero, source = IntPtr.Zero, loop = IntPtr.Zero;
        bool registered = false;
        try
        {
            if (Marshal.SizeOf<MacContact>() != 96) throw new InvalidOperationException("Unexpected contact layout.");
            mode = MacTouchpadNative.CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", 0x08000100);
            loop = MacTouchpadNative.CFRunLoopGetCurrent();
            tap = MacTouchpadNative.CGEventTapCreate(1, 0, 0, (1UL << 5) | (1UL << 6) | (1UL << 7) | (1UL << 27), tapCallback, IntPtr.Zero);
            if (tap == IntPtr.Zero) throw new InvalidOperationException("Grant OpenTabletDriver Accessibility and Input Monitoring permission, then reopen it.");
            source = MacTouchpadNative.CFMachPortCreateRunLoopSource(IntPtr.Zero, tap, 0);
            if (source == IntPtr.Zero) throw new InvalidOperationException("Could not create mouse event source.");
            MacTouchpadNative.CFRunLoopAddSource(loop, source, mode);
            MacTouchpadNative.MTRegisterContactFrameCallback(device, callback);
            registered = true;
            MacTouchpadNative.MTDeviceStart(device, 0);
            Log.Write("Touchpad", "Apple trackpad connected in single-finger absolute mode.");
            ready.Set();
            while (!stopping) MacTouchpadNative.CFRunLoopRunInMode(mode, 0.02, false);
        }
        catch (Exception ex) { startupError = ex; Log.Exception(ex); }
        finally
        {
            stopping = true;
            Interlocked.Exchange(ref lastContact, 0);
            global::AsusTouchpad.TouchpadControl.Reset();
            if (registered) { MacTouchpadNative.MTDeviceStop(device); MacTouchpadNative.MTUnregisterContactFrameCallback(device, callback); }
            if (tap != IntPtr.Zero) MacTouchpadNative.CFMachPortInvalidate(tap);
            if (source != IntPtr.Zero) { MacTouchpadNative.CFRunLoopRemoveSource(loop, source, mode); MacTouchpadNative.CFRelease(source); }
            if (tap != IntPtr.Zero) MacTouchpadNative.CFRelease(tap);
            if (mode != IntPtr.Zero) MacTouchpadNative.CFRelease(mode);
            reports.Dispose();
            ready.Set();
        }
    }
    public byte[] Read() => reports.Read();
    public void Write(byte[] buffer) => throw new NotSupportedException();
    public void GetFeature(byte[] buffer) => throw new NotSupportedException();
    public void SetFeature(byte[] buffer) => throw new NotSupportedException();
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stopping = true;
        reports.Dispose();
        if (Thread.CurrentThread != thread) { thread.Join(); ready.Dispose(); }
    }
}

internal static class MacTouchpadNative
{
    const string MT = "/System/Library/PrivateFrameworks/MultitouchSupport.framework/MultitouchSupport";
    const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FrameCallback(int sender, IntPtr contacts, int count, double timestamp, int frame);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr TapCallback(IntPtr proxy, uint type, IntPtr evt, IntPtr context);
    [DllImport(MT)] internal static extern IntPtr MTDeviceCreateList();
    [DllImport(MT)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool MTDeviceIsBuiltIn(IntPtr device);
    [DllImport(MT)] internal static extern int MTDeviceGetSensorSurfaceDimensions(IntPtr device, out int width, out int height);
    [DllImport(MT)] internal static extern void MTRegisterContactFrameCallback(IntPtr device, FrameCallback callback);
    [DllImport(MT)] internal static extern void MTUnregisterContactFrameCallback(IntPtr device, FrameCallback callback);
    [DllImport(MT)] internal static extern void MTDeviceStart(IntPtr device, int mode);
    [DllImport(MT)] internal static extern void MTDeviceStop(IntPtr device);
    [DllImport(CF)] internal static extern nint CFArrayGetCount(IntPtr array);
    [DllImport(CF)] internal static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [DllImport(CF)] internal static extern IntPtr CFRetain(IntPtr value);
    [DllImport(CF)] internal static extern void CFRelease(IntPtr value);
    [DllImport(CF)] internal static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);
    [DllImport(CF)] internal static extern IntPtr CFRunLoopGetCurrent();
    [DllImport(CF)] internal static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSource);
    [DllImport(CF)] internal static extern void CFRunLoopAddSource(IntPtr loop, IntPtr source, IntPtr mode);
    [DllImport(CF)] internal static extern void CFRunLoopRemoveSource(IntPtr loop, IntPtr source, IntPtr mode);
    [DllImport(CF)] internal static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, nint order);
    [DllImport(CF)] internal static extern void CFMachPortInvalidate(IntPtr port);
    [DllImport(CG)] internal static extern IntPtr CGEventTapCreate(int location, int placement, int options, ulong mask, TapCallback callback, IntPtr context);
    [DllImport(CG)] internal static extern long CGEventGetIntegerValueField(IntPtr evt, int field);
    [DllImport(CG)] internal static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enabled);
}
