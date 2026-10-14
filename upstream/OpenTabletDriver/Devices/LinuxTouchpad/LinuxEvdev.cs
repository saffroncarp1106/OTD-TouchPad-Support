using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenTabletDriver.Devices.LinuxTouchpad;

internal static class LinuxEvdev
{
    private const string Lib = "libevdev.so.2";
    internal const uint EV_SYN = 0, EV_KEY = 1, EV_REL = 2, EV_ABS = 3;
    internal const uint SYN_REPORT = 0, ABS_X = 0, ABS_Y = 1, ABS_MT_SLOT = 47,
        ABS_MT_POSITION_X = 53, ABS_MT_POSITION_Y = 54, ABS_MT_TOOL_TYPE = 55, ABS_MT_TRACKING_ID = 57;
    internal const uint BTN_TOUCH = 330, BTN_TOOL_FINGER = 325;
    internal const int EAGAIN = 11, EINTR = 4;
    [StructLayout(LayoutKind.Sequential)]
    internal struct InputEvent { public nint Seconds, Microseconds; public ushort Type, Code; public int Value; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AbsInfo { public int Value, Min, Max, Fuzz, Flat, Resolution; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PollFd { public int Fd; public short Events, Revents; }
    [DllImport("libc", SetLastError = true)] internal static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", SetLastError = true)] internal static extern int close(int fd);
    [DllImport("libc", SetLastError = true)] internal static extern int poll(ref PollFd fd, nuint count, int timeout);
    [DllImport(Lib)] internal static extern int libevdev_new_from_fd(int fd, out IntPtr dev);
    [DllImport(Lib)] internal static extern void libevdev_free(IntPtr dev);
    [DllImport(Lib)] internal static extern IntPtr libevdev_get_name(IntPtr dev);
    [DllImport(Lib)] internal static extern IntPtr libevdev_get_phys(IntPtr dev);
    [DllImport(Lib)] internal static extern IntPtr libevdev_get_uniq(IntPtr dev);
    [DllImport(Lib)] internal static extern int libevdev_get_id_vendor(IntPtr dev);
    [DllImport(Lib)] internal static extern int libevdev_get_id_product(IntPtr dev);
    [DllImport(Lib)] internal static extern int libevdev_get_id_bustype(IntPtr dev);
    [DllImport(Lib)] internal static extern int libevdev_has_property(IntPtr dev, uint prop);
    [DllImport(Lib)] internal static extern int libevdev_has_event_code(IntPtr dev, uint type, uint code);
    [DllImport(Lib)] internal static extern IntPtr libevdev_get_abs_info(IntPtr dev, uint code);
    [DllImport(Lib)] internal static extern int libevdev_get_num_slots(IntPtr dev);
    [DllImport(Lib)] internal static extern int libevdev_get_slot_value(IntPtr dev, uint slot, uint code);
    [DllImport(Lib)] internal static extern int libevdev_get_event_value(IntPtr dev, uint type, uint code);
    [DllImport(Lib)] internal static extern int libevdev_next_event(IntPtr dev, uint flags, out InputEvent evt);
    [DllImport(Lib)] internal static extern int libevdev_grab(IntPtr dev, int grab);
    [DllImport(Lib)] internal static extern IntPtr libevdev_new();
    [DllImport(Lib)] internal static extern int libevdev_enable_property(IntPtr dev, uint property);
    [DllImport(Lib)] internal static extern IntPtr libevdev_uinput_get_devnode(IntPtr dev);
    [DllImport(Lib)] internal static extern void libevdev_set_name(IntPtr dev, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] internal static extern int libevdev_enable_event_code(IntPtr dev, uint type, uint code, IntPtr data);
    [DllImport(Lib)] internal static extern int libevdev_uinput_create_from_device(IntPtr dev, int fd, out IntPtr uinput);
    [DllImport(Lib)] internal static extern int libevdev_uinput_write_event(IntPtr dev, uint type, uint code, int value);
    [DllImport(Lib)] internal static extern void libevdev_uinput_destroy(IntPtr dev);
    internal static string Text(IntPtr value) => Marshal.PtrToStringUTF8(value) ?? "";
    internal static LinuxAxis Axis(IntPtr dev, uint code)
    {
        var ptr = libevdev_get_abs_info(dev, code);
        if (ptr == IntPtr.Zero) return default;
        var info = Marshal.PtrToStructure<AbsInfo>(ptr);
        return new LinuxAxis(info.Min, info.Max, info.Resolution);
    }
    internal static void Check(int result, string operation)
    {
        if (result < 0) throw new IOException($"{operation}: {new Win32Exception(-result).Message} (errno {-result})");
    }
}

internal sealed class LinuxInputHandle : IDisposable
{
    internal int Fd { get; private set; } = -1;
    internal IntPtr Device { get; private set; }
    internal LinuxInputHandle(string path)
    {
        // O_RDONLY | O_NONBLOCK | O_CLOEXEC; reading and grabbing need no writes.
        Fd = LinuxEvdev.open(path, 0x800 | 0x80000);
        if (Fd < 0) throw new IOException($"Cannot open {path}: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}. Run Prepare Linux.sh.");
        int status = LinuxEvdev.libevdev_new_from_fd(Fd, out var device);
        Device = device;
        if (status < 0) { Dispose(); LinuxEvdev.Check(status, "Initialize evdev"); }
    }
    ~LinuxInputHandle() => Dispose();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Device != IntPtr.Zero) { LinuxEvdev.libevdev_free(Device); Device = IntPtr.Zero; }
        if (Fd >= 0) { LinuxEvdev.close(Fd); Fd = -1; }
    }
}

// A separate button-only mouse preserves physical clicking during EVIOCGRAB.
// It cannot generate competing pointer movement or be rediscovered as a touchpad.
internal sealed class LinuxTouchpadButtons : IDisposable
{
    private IntPtr device, output;
    private readonly HashSet<uint> supported = new();
    private readonly HashSet<uint> down = new();
    internal LinuxTouchpadButtons(IntPtr input)
    {
        device = LinuxEvdev.libevdev_new();
        if (device == IntPtr.Zero) throw new IOException("Cannot allocate touchpad button device");
        try
        {
            LinuxEvdev.libevdev_set_name(device, "OpenTabletDriver Touchpad Buttons");
            foreach (uint code in new uint[] { 272, 273, 274, 275, 276 })
                if (LinuxEvdev.libevdev_has_event_code(input, LinuxEvdev.EV_KEY, code) != 0)
                {
                    LinuxEvdev.Check(LinuxEvdev.libevdev_enable_event_code(device, LinuxEvdev.EV_KEY, code, IntPtr.Zero), "Enable touchpad button");
                    supported.Add(code);
                }
            if (supported.Count == 0) return;
            LinuxEvdev.Check(LinuxEvdev.libevdev_enable_event_code(device, LinuxEvdev.EV_REL, 0, IntPtr.Zero), "Enable mouse X");
            LinuxEvdev.Check(LinuxEvdev.libevdev_enable_event_code(device, LinuxEvdev.EV_REL, 1, IntPtr.Zero), "Enable mouse Y");
            LinuxEvdev.Check(LinuxEvdev.libevdev_uinput_create_from_device(device, -2, out output), "Create button output; check /dev/uinput permissions");
        }
        catch { Dispose(); throw; }
    }
    internal void Synchronize(IntPtr input)
    {
        if (output == IntPtr.Zero) return;
        bool dirty = false;
        foreach (uint code in supported)
        {
            bool pressed = LinuxEvdev.libevdev_get_event_value(input, LinuxEvdev.EV_KEY, code) != 0;
            if (pressed == down.Contains(code)) continue;
            LinuxEvdev.Check(LinuxEvdev.libevdev_uinput_write_event(output, LinuxEvdev.EV_KEY, code, pressed ? 1 : 0), "Write touchpad click");
            if (pressed) down.Add(code); else down.Remove(code);
            dirty = true;
        }
        if (dirty) LinuxEvdev.Check(LinuxEvdev.libevdev_uinput_write_event(output, LinuxEvdev.EV_SYN, 0, 0), "Flush touchpad click");
    }
    internal void Release()
    {
        if (output == IntPtr.Zero) return;
        foreach (uint code in down) LinuxEvdev.libevdev_uinput_write_event(output, LinuxEvdev.EV_KEY, code, 0);
        down.Clear();
        LinuxEvdev.libevdev_uinput_write_event(output, LinuxEvdev.EV_SYN, 0, 0);
    }
    ~LinuxTouchpadButtons() => Dispose();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Release();
        if (output != IntPtr.Zero) { LinuxEvdev.libevdev_uinput_destroy(output); output = IntPtr.Zero; }
        if (device != IntPtr.Zero) { LinuxEvdev.libevdev_free(device); device = IntPtr.Zero; }
    }
}
