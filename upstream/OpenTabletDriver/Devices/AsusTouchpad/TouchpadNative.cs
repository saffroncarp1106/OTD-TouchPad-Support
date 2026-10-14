#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AsusTouchpad;
public sealed partial class TouchpadDevice : IDisposable
{
    public IntPtr Handle;
    public string Path = "";
    public IntPtr Descriptor;
    public readonly List<Axis> Axes = new();
    public record Axis(ushort Link, ushort Usage, int Min, int Max, int PhysicalMin, int PhysicalMax, uint Units, int Exponent, byte ReportId = 0, int BitSize = 16);
    public void Dispose()
    {
        if (Descriptor != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Descriptor);
            Descriptor = IntPtr.Zero;
        }
    }

    public override string ToString() => Path + "\n" + string.Join("\n", Axes);
}

public record TouchPoint(uint Id, ushort X, ushort Y);
public static class TouchpadControl
{
    public static volatile bool Enabled = true;
    public static long LastContact;
    public static void Reset()
    {
        Interlocked.Exchange(ref LastContact, 0);
        Interlocked.Exchange(ref lastOutput, 0);
    }

    private static long lastOutput;
    public static void NotifyAbsoluteOutput() => Interlocked.Exchange(ref lastOutput, Environment.TickCount64);
    public static bool ShouldSuppress => Enabled && Environment.TickCount64 - Interlocked.Read(ref LastContact) < 100 && Environment.TickCount64 - Interlocked.Read(ref lastOutput) < 100;
}

public static partial class TouchpadNative
{
    const int Success = 0x110000;
    [StructLayout(LayoutKind.Sequential)]
    struct RawDevice
    {
        public IntPtr Handle;
        public uint Type;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRawInputDeviceList([Out] RawDevice[]? list, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
    [DllImport("hid.dll")]
    static extern int HidP_GetValueCaps(int type, IntPtr caps, ref ushort count, IntPtr data);
    [DllImport("hid.dll")]
    static extern int HidP_GetUsageValue(int type, ushort page, ushort link, ushort usage, out uint value, IntPtr data, byte[] report, uint length);
    [DllImport("hid.dll")]
    static extern int HidP_GetUsages(int type, ushort page, ushort link, [Out] ushort[] usages, ref uint count, IntPtr data, byte[] report, uint length);
    public static ushort Normalize(uint value, int min, int max) => max <= min ? throw new ArgumentException("Invalid axis bounds") : (ushort)Math.Round(Math.Clamp(((double)value - min) / (max - min), 0, 1) * 65535);
    public static uint ContactCount(TouchpadDevice device, byte[] report)
    {
        return HidP_GetUsageValue(0, 13, 0, 0x54, out uint count, device.Descriptor, report, (uint)report.Length) == Success ? count : 0;
    }

    public static List<TouchPoint> Parse(TouchpadDevice device, byte[] report)
    {
        var points = new List<TouchPoint>();
        foreach (var x in device.Axes.Where(a => a.Usage == 0x30))
        {
            var y = device.Axes.FirstOrDefault(a => a.Link == x.Link && a.Usage == 0x31);
            if (y == null)
                continue;
            ushort[] buttons = new ushort[32];
            uint count = 32;
            if (HidP_GetUsages(0, 13, x.Link, buttons, ref count, device.Descriptor, report, (uint)report.Length) != Success)
                continue;
            if (!buttons.Take((int)count).Contains((ushort)0x42))
                continue;
            if (!buttons.Take((int)count).Contains((ushort)0x47))
                continue;
            if (HidP_GetUsageValue(0, 1, x.Link, 0x30, out uint vx, device.Descriptor, report, (uint)report.Length) != Success || HidP_GetUsageValue(0, 1, x.Link, 0x31, out uint vy, device.Descriptor, report, (uint)report.Length) != Success || HidP_GetUsageValue(0, 13, x.Link, 0x51, out uint id, device.Descriptor, report, (uint)report.Length) != Success)
                continue;
            points.Add(new(id, Normalize(vx, x.Min, x.Max), Normalize(vy, y.Min, y.Max)));
        }

        return points;
    }
}

public sealed class TouchpadReceiver : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct InputDevice
    {
        public ushort Page, Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WindowClass
    {
        public uint Style;
        public WndProc Proc;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public string? Menu;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr W;
        public IntPtr L;
        public uint Time;
        public int X, Y;
        public uint Private;
    }

    delegate IntPtr WndProc(IntPtr window, uint message, UIntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClass(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr DefWindowProc(IntPtr h, uint msg, UIntPtr w, IntPtr l);
    [DllImport("user32.dll")]
    static extern int GetMessage(out Message m, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage(ref Message m);
    [DllImport("user32.dll")]
    static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr h, uint m, UIntPtr w, IntPtr l);
    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool UnregisterClass(string name, IntPtr instance);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices(InputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint header);
    delegate IntPtr MouseProc(int code, UIntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int id, MouseProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr window, int id);
    readonly bool integration;
    readonly bool suppressRelative;
    readonly MouseProc mouseProc;
    IntPtr mouseHook;
    IntPtr MouseHook(int code, UIntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToUInt64() == 0x200 && (Marshal.ReadInt32(data, 12) & 1) == 0 && TouchpadControl.ShouldSuppress)
            return new IntPtr(1);
        return CallNextHookEx(mouseHook, code, message, data);
    }

    readonly TouchpadDevice device;
    readonly Thread thread;
    readonly ManualResetEventSlim ready = new();
    readonly WndProc proc;
    IntPtr window;
    Exception? startup;
    bool disposed;
    public event Action<byte[], List<TouchPoint>>? Report;
    public event Action<Exception>? Error;
    public event Action<TouchFrame>? Frame;
    public event Action? Removed;
    public TouchpadReceiver(TouchpadDevice device, bool integration = false, bool suppressRelative = true)
    {
        this.device = device;
        this.integration = integration;
        this.suppressRelative = suppressRelative;
        mouseProc = MouseHook;
        proc = WindowProc;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Precision touchpad Raw Input"
        };
        thread.Start();
        if (!ready.Wait(10000))
            throw new TimeoutException("Raw Input startup");
        if (startup != null)
            throw new InvalidOperationException("Raw Input startup failed", startup);
    }

    void Run()
    {
        string name = "AsusTouchpad_" + Guid.NewGuid().ToString("N");
        IntPtr module = GetModuleHandle(null);
        bool registered = false;
        try
        {
            var cls = new WindowClass
            {
                Proc = proc,
                Instance = module,
                Name = name
            };
            if (RegisterClass(ref cls) == 0)
                throw new Win32Exception();
            registered = true;
            window = CreateWindowEx(0, name, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, module, IntPtr.Zero);
            if (window == IntPtr.Zero)
                throw new Win32Exception();
            if (!RegisterRawInputDevices(new[] { new InputDevice { Page = 13, Usage = 5, Flags = 0x2100, Target = window } }, 1, (uint)Marshal.SizeOf<InputDevice>()))
                throw new Win32Exception();
            if (integration)
            {
                if (suppressRelative)
                {
                    mouseHook = SetWindowsHookEx(14, mouseProc, module, 0);
                    if (mouseHook == IntPtr.Zero)
                        throw new Win32Exception();
                }

                if (!RegisterHotKey(window, 1, 0x4003, 0x7B))
                    throw new Win32Exception();
            }

            ready.Set();
            int status;
            while ((status = GetMessage(out var msg, IntPtr.Zero, 0, 0)) > 0)
                DispatchMessage(ref msg);
            if (status < 0)
                throw new Win32Exception();
        }
        catch (Exception ex)
        {
            startup = ex;
            Error?.Invoke(ex);
        }
        finally
        {
            ready.Set();
            if (mouseHook != IntPtr.Zero)
                UnhookWindowsHookEx(mouseHook);
            if (integration && window != IntPtr.Zero)
                UnregisterHotKey(window, 1);
            if (window != IntPtr.Zero)
            {
                DestroyWindow(window);
                window = IntPtr.Zero;
            }

            if (registered)
                UnregisterClass(name, module);
        }
    }

    IntPtr WindowProc(IntPtr h, uint msg, UIntPtr w, IntPtr l)
    {
        if (msg == 0x0010)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        if (msg == 0x312 && integration)
        {
            TouchpadControl.Enabled = !TouchpadControl.Enabled;
            TouchpadControl.Reset();
            Frame?.Invoke(new TouchFrame(new(), 0, null));
            Report?.Invoke(Array.Empty<byte>(), new List<TouchPoint>());
            return IntPtr.Zero;
        }

        if (msg == 0xfe && w.ToUInt64() == 2 && l == device.Handle)
        {
            TouchpadControl.Reset();
            Removed?.Invoke();
        }

        if (msg == 0xff)
        {
            try
            {
                Read(l);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
        }

        return DefWindowProc(h, msg, w, l);
    }

    void Read(IntPtr raw)
    {
        uint size = 0, header = (uint)(8 + 2 * IntPtr.Size);
        if (GetRawInputData(raw, 0x10000003, IntPtr.Zero, ref size, header) == uint.MaxValue || size < header + 8 || size > 1048576)
            return;
        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(raw, 0x10000003, buffer, ref size, header) == uint.MaxValue)
                throw new Win32Exception();
            if (Marshal.ReadInt32(buffer) != 2 || Marshal.ReadIntPtr(buffer, 8) != device.Handle)
                return;
            int length = Marshal.ReadInt32(buffer, (int)header), count = Marshal.ReadInt32(buffer, (int)header + 4);
            if (length <= 0 || count < 0 || (long)length * count > size - header - 8)
                return;
            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[length];
                Marshal.Copy(IntPtr.Add(buffer, (int)header + 8 + i * length), bytes, 0, length);
                var frame = TouchpadNative.Decode(device, bytes);
                if (frame != null)
                {
                    if (frame.Points.Count == 1 && frame.ContactCount == 1)
                        Interlocked.Exchange(ref TouchpadControl.LastContact, Environment.TickCount64);
                    else
                        TouchpadControl.Reset();
                    Frame?.Invoke(frame);
                    Report?.Invoke(bytes, frame.Points);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (window != IntPtr.Zero)
            PostMessage(window, 0x10, UIntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != thread)
            thread.Join();
        ready.Dispose();
    }
}