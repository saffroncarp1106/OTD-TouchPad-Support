using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AsusTouchpad;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Devices;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Devices.AsusTouchpad
{
    [DeviceHub, SupportedPlatform(PluginPlatform.Windows)]
    public sealed class AsusTouchpadHub : IDeviceHub
    {
        private readonly Timer watcher;
        private IDeviceEndpoint[] previous = Array.Empty<IDeviceEndpoint>();
        private int checking;
        public event EventHandler<DevicesChangedEventArgs> DevicesChanged;
        public AsusTouchpadHub()
        {
            watcher = new Timer(_ => Poll(), null, 2000, 2000);
        }

        private void Poll()
        {
            if (Interlocked.Exchange(ref checking, 1) != 0)
                return;
            try
            {
                var current = TouchpadSelection.Refresh();
                var changes = new DevicesChangedEventArgs(previous, current);
                previous = current;
                if (changes.Changes.Any())
                    DevicesChanged?.Invoke(this, changes);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            finally
            {
                Volatile.Write(ref checking, 0);
            }
        }

        public IEnumerable<IDeviceEndpoint> GetDevices() => TouchpadSelection.Current;
    }

    public static class TouchpadSelection
    {
        static readonly object sync = new();
        static AsusEndpoint[] endpoints = Array.Empty<AsusEndpoint>();
        public static IEnumerable<IDeviceEndpoint> Current
        {
            get
            {
                lock (sync)
                    return endpoints.ToArray();
            }
        }

        public static IDeviceEndpoint[] Refresh()
        {
            lock (sync)
            {
                var devices = TouchpadNative.Enumerate();
                try
                {
                    var profile = TouchpadProfile.Load();
                    var selected = TouchpadProfile.Resolve(devices, profile);
                    if (selected == null)
                    {
                        endpoints = Array.Empty<AsusEndpoint>();
                        return endpoints;
                    }

                    profile ??= TouchpadProfile.Save(selected);
                    if (!profile.ValidDimensions)
                        throw new InvalidOperationException("Invalid saved touchpad dimensions; run setup again.");
                    endpoints = new[]
                    {
                        new AsusEndpoint(selected, profile)
                    };
                    return endpoints;
                }
                finally
                {
                    foreach (var device in devices)
                        device.Dispose();
                }
            }
        }

        public static IEnumerable<TabletConfiguration> Configurations()
        {
            if (!OperatingSystem.IsWindows())
                return Array.Empty<TabletConfiguration>();
            Refresh();
            lock (sync)
                return endpoints.Select(e => new TabletConfiguration { Name = e.ProductName, Attributes = new Dictionary<string, string> { { "PrecisionTouchpad", "true" } }, Specifications = new TabletSpecifications { Digitizer = new DigitizerSpecifications { Width = e.Profile.Width, Height = e.Profile.Height, MaxX = 65535, MaxY = 65535 }, Pen = new PenSpecifications { MaxPressure = 1, ButtonCount = 0 } }, DigitizerIdentifiers = new List<DeviceIdentifier> { new DeviceIdentifier { VendorID = 65534, ProductID = 1209, InputReportLength = 9, ReportParser = "OpenTabletDriver.Configurations.Parsers.ASUS.AsusTouchpadReportParser" } } }).ToArray();
        }
    }

    internal sealed class AsusEndpoint : IDeviceEndpoint
    {
        public readonly string Path, Signature;
        public readonly TouchpadProfile Profile;
        public AsusEndpoint(TouchpadDevice device, TouchpadProfile profile)
        {
            Path = device.Path;
            Signature = device.Signature;
            Profile = profile;
            DevicePath = "rawinput:" + Path + ":" + device.Handle.ToInt64().ToString("X") + ":" + Signature;
            ProductName = $"{device.Name} [{device.Vendor:X4}:{device.Product:X4}] (Absolute)";
        }

        public int VendorID => 65534;
        public int ProductID => 1209;
        public int InputReportLength => 9;
        public int OutputReportLength => 0;
        public int FeatureReportLength => 0;
        public string Manufacturer => "Windows Precision Touchpad";
        public string ProductName { get; }
        public string FriendlyName => ProductName;
        public string SerialNumber => Signature;
        public string DevicePath { get; }
        public bool CanOpen => true;
        public IDictionary<string, string> DeviceAttributes => new Dictionary<string, string>
        {
            {
                "Transport",
                "Windows Raw Input"
            }
        };

        public string GetDeviceString(byte index) => index == 1 ? Manufacturer : ProductName;
        public IDeviceEndpointStream Open() => new AsusStream(this);
    }

    internal sealed class AsusStream : IDeviceEndpointStream
    {
        readonly LatestTouchpadReports queue = new();
        readonly Action<byte[]> receive;
        public AsusStream(AsusEndpoint endpoint)
        {
            receive = queue.Enqueue;
            AsusBroker.Subscribe(endpoint, receive, queue.Dispose);
        }

        public byte[] Read() => queue.Read();
        public void Write(byte[] buffer) => throw new NotSupportedException();
        public void GetFeature(byte[] buffer) => throw new NotSupportedException();
        public void SetFeature(byte[] buffer) => throw new NotSupportedException();
        bool disposed;
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            queue.Dispose();
            AsusBroker.Unsubscribe(receive, queue.Dispose);
        }
    }

    internal static class AsusBroker
    {
        static readonly object sync = new();
        static event Action<byte[]> Report;
        static event Action Removed;
        static TouchpadReceiver receiver;
        static TouchpadDevice device;
        static uint? primary;
        static bool hadContact;
        public static void Subscribe(AsusEndpoint endpoint, Action<byte[]> callback, Action removed)
        {
            lock (sync)
            {
                if (receiver == null)
                {
                    var candidates = TouchpadNative.Enumerate();
                    device = candidates.FirstOrDefault(d => d.Path == endpoint.Path && d.Signature == endpoint.Signature);
                    foreach (var candidate in candidates)
                        if (candidate != device)
                            candidate.Dispose();
                    if (device == null)
                        throw new InvalidOperationException("Selected touchpad disappeared or its descriptor changed.");
                    try
                    {
                        receiver = new TouchpadReceiver(device, integration: true, suppressRelative: endpoint.Profile.SuppressRelativeMovement);
                        receiver.Frame += OnFrame;
                        receiver.Error += ex => Log.Exception(ex);
                        receiver.Removed += OnRemoved;
                    }
                    catch
                    {
                        device.Dispose();
                        device = null;
                        throw;
                    }

                    Log.Write("Touchpad", $"Connected {endpoint.ProductName}; {endpoint.Profile.Width} x {endpoint.Profile.Height} mm. Single-finger absolute mode; Ctrl+Alt+F12 toggles mapping.");
                }
                else if (device.Path != endpoint.Path || device.Signature != endpoint.Signature)
                    throw new InvalidOperationException("Stop the current touchpad before switching devices.");
                Report += callback;
                Removed += removed;
            }
        }

        static void OnRemoved()
        {
            TouchpadControl.Reset();
            Removed?.Invoke();
        }

        public static void Unsubscribe(Action<byte[]> callback, Action removed)
        {
            TouchpadReceiver old = null;
            TouchpadDevice oldDevice = null;
            lock (sync)
            {
                Report -= callback;
                Removed -= removed;
                if (Report == null)
                {
                    old = receiver;
                    oldDevice = device;
                    receiver = null;
                    device = null;
                    primary = null;
                    hadContact = false;
                }
            }

            old?.Dispose();
            oldDevice?.Dispose();
            TouchpadControl.Reset();
        }

        static void OnFrame(TouchFrame frame)
        {
            lock (sync)
            {
                var points = frame.Points;
                if (!TouchpadControl.Enabled || frame.ContactCount != 1 || points.Count != 1)
                {
                    if (hadContact)
                        Report?.Invoke(new byte[9]);
                    hadContact = false;
                    primary = null;
                    return;
                }

                var point = points[0];
                if (primary.HasValue && primary.Value != point.Id)
                    Report?.Invoke(new byte[9]);
                primary = point.Id;
                hadContact = true;
                Report?.Invoke(new byte[] { 1, 0, (byte)point.X, (byte)(point.X >> 8), (byte)point.Y, (byte)(point.Y >> 8), 0, 0, 0 });
            }
        }
    }
}