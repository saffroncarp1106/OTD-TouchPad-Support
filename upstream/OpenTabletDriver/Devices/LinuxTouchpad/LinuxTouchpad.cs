using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Devices;
using OpenTabletDriver.Plugin.Tablet;
using static OpenTabletDriver.Devices.LinuxTouchpad.LinuxEvdev;

namespace OpenTabletDriver.Devices.LinuxTouchpad;

[DeviceHub, SupportedPlatform(PluginPlatform.Linux)]
public sealed class LinuxTouchpadHub : IDeviceHub, IDisposable
{
    private readonly Timer watcher;
    private IDeviceEndpoint[] previous;
    private int checking;
    public event EventHandler<DevicesChangedEventArgs> DevicesChanged;
    public LinuxTouchpadHub()
    {
        previous = LinuxTouchpadSelection.Refresh();
        watcher = new Timer(_ => Poll(), null, 2000, 2000);
    }
    private void Poll()
    {
        if (Interlocked.Exchange(ref checking, 1) != 0) return;
        try
        {
            var current = LinuxTouchpadSelection.Refresh();
            var changes = new DevicesChangedEventArgs(previous, current);
            previous = current;
            if (changes.Changes.Any()) DevicesChanged?.Invoke(this, changes);
        }
        catch (Exception ex) { Log.Exception(ex); }
        finally { Volatile.Write(ref checking, 0); }
    }
    public IEnumerable<IDeviceEndpoint> GetDevices() => LinuxTouchpadSelection.Current;
    public void Dispose() => watcher.Dispose();
}

public static class LinuxTouchpadSelection
{
    private static readonly object Sync = new();
    private static LinuxTouchpadEndpoint[] endpoints = Array.Empty<LinuxTouchpadEndpoint>();
    private static readonly HashSet<string> warnings = new();
    internal static void WarnOnce(string message)
    {
        lock (Sync) if (warnings.Add(message)) Log.Write("Linux Touchpad", message, LogLevel.Warning);
    }
    internal static IDeviceEndpoint[] Current { get { lock (Sync) return endpoints.ToArray(); } }
    internal static IDeviceEndpoint[] Refresh()
    {
        if (!OperatingSystem.IsLinux()) return Array.Empty<IDeviceEndpoint>();
        lock (Sync)
        {
            var found = new List<LinuxTouchpadEndpoint>();
            if (!Directory.Exists("/dev/input")) return endpoints = Array.Empty<LinuxTouchpadEndpoint>();
            var chosen = Environment.GetEnvironmentVariable("OTD_TOUCHPAD_DEVICE");
            foreach (var path in Directory.EnumerateFiles("/dev/input", "event*").OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    using var handle = new LinuxInputHandle(path);
                    var endpoint = LinuxTouchpadEndpoint.Discover(path, handle.Device);
                    if (endpoint != null && (string.IsNullOrEmpty(chosen) || chosen == path || chosen == endpoint.Identity))
                        found.Add(endpoint);
                }
                catch (IOException)
                {
                    // Keyboard permissions need not be granted. Only report
                    // inaccessible nodes identified as touchpads by udev.
                    string db = "/run/udev/data/c";
                    string node = Path.GetFileName(path);
                    string dev = "/sys/class/input/" + node + "/dev";
                    if (File.Exists(dev) && File.Exists(db + File.ReadAllText(dev).Trim()) &&
                        File.ReadAllText(db + File.ReadAllText(dev).Trim()).Contains("ID_INPUT_TOUCHPAD=1"))
                        WarnOnce($"Cannot access touchpad {path}. Run Prepare Linux.sh, then restart OTD.");
                }
                catch (DllNotFoundException)
                {
                    WarnOnce("libevdev.so.2 is missing. Install libevdev2 using your distribution package manager.");
                    break;
                }
                catch (Exception ex) { WarnOnce($"Touchpad discovery failed for {path}: {ex.Message}"); }
            }
            return endpoints = found.ToArray();
        }
    }
    public static IEnumerable<TabletConfiguration> Configurations()
    {
        if (!OperatingSystem.IsLinux()) return Array.Empty<TabletConfiguration>();
        lock (Sync)
            return endpoints.Select(e => new TabletConfiguration
            {
                Name = e.ProductName,
                Attributes = new Dictionary<string, string>
                {
                    ["PrecisionTouchpad"] = "true", ["LinuxTouchpadIdentity"] = e.Identity
                },
                Specifications = new TabletSpecifications
                {
                    Digitizer = new DigitizerSpecifications { Width = e.Width, Height = e.Height, MaxX = 65535, MaxY = 65535 },
                    Pen = new PenSpecifications { MaxPressure = 1, ButtonCount = 0 }
                },
                DigitizerIdentifiers = new List<DeviceIdentifier>
                {
                    new DeviceIdentifier
                    {
                        VendorID = 65534, ProductID = 1211, InputReportLength = 9,
                        DeviceStrings = new Dictionary<byte, string> { [2] = "^" + Regex.Escape(e.ProductName) + "$" },
                        ReportParser = "OpenTabletDriver.Configurations.Parsers.ASUS.AsusTouchpadReportParser"
                    }
                }
            }).ToArray();
    }
}

// Claims follow output-mode lifetime, so disabled/relative modes release the
// hardware grab even when the input stream remains open.
public static class LinuxTouchpadControl
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, int> claims = new();
    public static void Acquire(string identity) { lock (Sync) { claims.TryGetValue(identity, out int count); claims[identity] = count + 1; } }
    public static void Release(string identity)
    {
        lock (Sync)
            if (claims.TryGetValue(identity, out int count))
                if (count <= 1) claims.Remove(identity); else claims[identity] = count - 1;
    }
    internal static bool Active(string identity) { lock (Sync) return claims.ContainsKey(identity); }
}

internal sealed class LinuxTouchpadEndpoint : IDeviceEndpoint
{
    internal string Path, Identity, Signature;
    internal LinuxAxis XAxis, YAxis;
    internal bool Multitouch;
    internal float Width, Height;
    internal static LinuxTouchpadEndpoint Discover(string path, IntPtr dev)
    {
        string name = Text(libevdev_get_name(dev));
        if (name.StartsWith("OpenTabletDriver", StringComparison.Ordinal) || libevdev_has_property(dev, 1) != 0 || libevdev_has_property(dev, 5) != 0)
            return null; // INPUT_PROP_DIRECT / INPUT_PROP_POINTING_STICK
        if (libevdev_has_event_code(dev, EV_KEY, BTN_TOOL_FINGER) == 0 || libevdev_has_event_code(dev, EV_KEY, BTN_TOUCH) == 0)
            return null;
        bool mt = libevdev_has_event_code(dev, EV_ABS, ABS_MT_SLOT) != 0;
        if (!mt && libevdev_has_event_code(dev, EV_ABS, ABS_MT_POSITION_X) != 0)
        {
            LinuxTouchpadSelection.WarnOnce($"{name}: legacy type A multitouch is not supported by this backend.");
            return null;
        }
        var x = Axis(dev, mt ? ABS_MT_POSITION_X : ABS_X);
        var y = Axis(dev, mt ? ABS_MT_POSITION_Y : ABS_Y);
        if (!x.Valid || !y.Valid || (mt && (libevdev_get_num_slots(dev) < 1 || libevdev_get_num_slots(dev) > 256 || libevdev_has_event_code(dev, EV_ABS, ABS_MT_TRACKING_ID) == 0)))
            return null;
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{libevdev_get_id_bustype(dev)}:{libevdev_get_id_vendor(dev)}:{libevdev_get_id_product(dev)}:{name}:{Text(libevdev_get_phys(dev))}:{Text(libevdev_get_uniq(dev))}")));
        string signature = $"{identity}:{x}:{y}:{libevdev_get_num_slots(dev)}";
        float width = x.Millimeters ?? 100;
        float height = y.Millimeters ?? (x.Millimeters.HasValue ? width : 100) * (float)((double)y.Max - y.Min) / (float)((double)x.Max - x.Min);
        if (!x.Millimeters.HasValue || !y.Millimeters.HasValue)
            LinuxTouchpadSelection.WarnOnce($"{name}: physical resolution is missing; using an estimated {width:F1} x {height:F1} mm area. Set OTD_TOUCHPAD_WIDTH_MM and OTD_TOUCHPAD_HEIGHT_MM if needed.");
        string widthOverride = Environment.GetEnvironmentVariable("OTD_TOUCHPAD_WIDTH_MM"), heightOverride = Environment.GetEnvironmentVariable("OTD_TOUCHPAD_HEIGHT_MM");
        if (widthOverride != null || heightOverride != null)
        {
            if (!float.TryParse(widthOverride, NumberStyles.Float, CultureInfo.InvariantCulture, out width) ||
                !float.TryParse(heightOverride, NumberStyles.Float, CultureInfo.InvariantCulture, out height) ||
                !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0 || width > 1000 || height > 1000)
                throw new InvalidOperationException("Set both OTD_TOUCHPAD_WIDTH_MM and OTD_TOUCHPAD_HEIGHT_MM to valid millimeter values.");
        }
        return new LinuxTouchpadEndpoint
        {
            Path = path, Identity = identity, Signature = signature, XAxis = x, YAxis = y, Multitouch = mt,
            Width = width, Height = height, ProductName = $"{name} [{identity[..6]}] (Absolute)"
        };
    }
    public int VendorID => 65534;
    public int ProductID => 1211;
    public int InputReportLength => 9;
    public int OutputReportLength => 0;
    public int FeatureReportLength => 0;
    public string Manufacturer => "Linux evdev Touchpad";
    public string ProductName { get; private set; }
    public string FriendlyName => ProductName;
    public string SerialNumber => Identity;
    public string DevicePath => "evdev:" + Path + ":" + Signature;
    public bool CanOpen => File.Exists(Path);
    public IDictionary<string, string> DeviceAttributes => new Dictionary<string, string> { ["Transport"] = "Linux evdev" };
    public string GetDeviceString(byte index) => index == 1 ? Manufacturer : ProductName;
    public IDeviceEndpointStream Open() => new LinuxTouchpadStream(this);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Native resources are disposed by the reader thread finally block; Dispose joins that thread.")]
internal sealed class LinuxTouchpadStream : IDeviceEndpointStream
{
    private readonly AsusTouchpad.LatestTouchpadReports reports = new();
    private readonly LinuxContactEncoder encoder = new();
    private readonly LinuxTouchpadEndpoint endpoint;
    private readonly LinuxInputHandle handle;
    private readonly LinuxTouchpadButtons buttons;
    private readonly Thread thread;
    private readonly bool flipY = Environment.GetEnvironmentVariable("OTD_TOUCHPAD_FLIP_Y") == "1";
    private volatile bool stopping;
    private bool grabbed;
    private int disposed;
    internal LinuxTouchpadStream(LinuxTouchpadEndpoint endpoint)
    {
        this.endpoint = endpoint;
        handle = new LinuxInputHandle(endpoint.Path);
        try
        {
            var actual = LinuxTouchpadEndpoint.Discover(endpoint.Path, handle.Device);
            if (actual?.Signature != endpoint.Signature) throw new IOException("Selected touchpad changed; detect tablets again.");
            buttons = new LinuxTouchpadButtons(handle.Device);
            thread = new Thread(Run) { IsBackground = true, Name = "Linux touchpad" };
            thread.Start();
            Log.Write("Linux Touchpad", $"Connected {endpoint.ProductName}; {endpoint.Width:F2} x {endpoint.Height:F2} mm. Select Absolute Mode to enable single-finger mapping.");
        }
        catch { buttons?.Dispose(); handle.Dispose(); reports.Dispose(); throw; }
    }
    private void Emit(byte[][] packets) { foreach (var packet in packets) reports.Enqueue(packet); }
    private List<LinuxContact> Contacts()
    {
        var dev = handle.Device;
        var contacts = new List<LinuxContact>();
        if (endpoint.Multitouch)
        {
            int count = libevdev_get_num_slots(dev);
            for (uint i = 0; i < count; i++)
            {
                int id = libevdev_get_slot_value(dev, i, ABS_MT_TRACKING_ID);
                if (id < 0) continue;
                contacts.Add(new LinuxContact(id, libevdev_get_slot_value(dev, i, ABS_MT_POSITION_X),
                    libevdev_get_slot_value(dev, i, ABS_MT_POSITION_Y), Palm:
                    libevdev_has_event_code(dev, EV_ABS, ABS_MT_TOOL_TYPE) != 0 && libevdev_get_slot_value(dev, i, ABS_MT_TOOL_TYPE) == 2));
            }
        }
        else if (libevdev_get_event_value(dev, EV_KEY, BTN_TOUCH) != 0)
            contacts.Add(new LinuxContact(0, libevdev_get_event_value(dev, EV_ABS, ABS_X), libevdev_get_event_value(dev, EV_ABS, ABS_Y)));
        // Some pads track fewer slots than physical fingers. BTN_TOOL_* count
        // hints must also suspend mapping, including on single-touch devices.
        if (new uint[] { 333, 334, 335, 328 }.Any(code => libevdev_get_event_value(dev, EV_KEY, code) != 0))
            contacts.Add(new LinuxContact(-1, 0, 0, Complete: false));
        return contacts;
    }
    private void Frame()
    {
        if (!grabbed) { Emit(encoder.Release()); return; }
        buttons.Synchronize(handle.Device);
        Emit(encoder.Encode(Contacts(), endpoint.XAxis, endpoint.YAxis, flipY));
    }
    private void Synchronize(bool force)
    {
        Emit(encoder.Release());
        if (force)
        {
            int first = libevdev_next_event(handle.Device, 4, out _); // FORCE_SYNC
            if (first != 1 && first != -EAGAIN) Check(first, "Force evdev synchronization");
        }
        int result;
        do { result = libevdev_next_event(handle.Device, 1, out _); } while (result == 1 && !stopping);
        if (result != -EAGAIN) Check(result, "Synchronize evdev state");
        Frame();
    }
    private void Run()
    {
        try
        {
            while (!stopping)
            {
                bool enabled = LinuxTouchpadControl.Active(endpoint.Identity);
                if (enabled != grabbed)
                {
                    if (enabled)
                    {
                        Check(libevdev_grab(handle.Device, 3), "Grab touchpad; another process may own it"); // LIBEVDEV_GRAB
                        grabbed = true;
                        Synchronize(true);
                        Log.Write("Linux Touchpad", "Absolute mapping enabled; hardware relative input suppressed. Physical clicks are forwarded.");
                    }
                    else
                    {
                        Emit(encoder.Release());
                        buttons.Release();
                        Check(libevdev_grab(handle.Device, 4), "Release touchpad"); // LIBEVDEV_UNGRAB
                        grabbed = false;
                        Log.Write("Linux Touchpad", "Absolute mapping disabled; normal touchpad input restored.");
                    }
                }
                // Drain libevdev's internal buffer before polling the fd.
                int result = libevdev_next_event(handle.Device, 2, out var evt); // NORMAL
                if (result == 1) { Synchronize(false); continue; }
                if (result == 0)
                {
                    if (evt.Type == EV_SYN && evt.Code == SYN_REPORT) Frame();
                    continue;
                }
                if (result != -EAGAIN && result != -EINTR) Check(result, "Read touchpad");
                var fd = new PollFd { Fd = handle.Fd, Events = 1 };
                int status = poll(ref fd, 1, 20);
                if (status < 0 && MarshalError() != EINTR) throw new IOException("Touchpad poll failed");
                if ((fd.Revents & (8 | 16 | 32)) != 0) throw new IOException("Touchpad disconnected");
            }
        }
        catch (Exception ex) { if (!stopping) Log.Exception(ex); }
        finally
        {
            stopping = true;
            buttons.Dispose();
            if (grabbed) libevdev_grab(handle.Device, 4);
            handle.Dispose(); // Kernel also releases EVIOCGRAB on process exit.
            reports.Dispose();
        }
    }
    private static int MarshalError() => System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
    public byte[] Read() => reports.Read();
    public void Write(byte[] buffer) => throw new NotSupportedException();
    public void GetFeature(byte[] buffer) => throw new NotSupportedException();
    public void SetFeature(byte[] buffer) => throw new NotSupportedException();
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stopping = true;
        reports.Dispose();
        if (Thread.CurrentThread != thread) thread.Join();
    }
}
