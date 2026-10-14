using System;
using System.Runtime.InteropServices;

public static class RawProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Device
    {
        public IntPtr Handle;
        public uint Type;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRawInputDeviceList([Out] Device[] list, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
    public static void Run()
    {
        uint count = 0;
        GetRawInputDeviceList(null, ref count, (uint)Marshal.SizeOf<Device>());
        var ds = new Device[count];
        GetRawInputDeviceList(ds, ref count, (uint)Marshal.SizeOf<Device>());
        foreach (var d in ds)
        {
            uint n = 0;
            GetRawInputDeviceInfo(d.Handle, 0x20000007, IntPtr.Zero, ref n);
            var p = Marshal.AllocHGlobal((int)n * 2 + 32);
            try
            {
                GetRawInputDeviceInfo(d.Handle, 0x20000007, p, ref n);
                string name = Marshal.PtrToStringUni(p);
                uint s = 32;
                Marshal.WriteInt32(p, 32);
                GetRawInputDeviceInfo(d.Handle, 0x2000000b, p, ref s);
                Console.WriteLine("{0} type={1} vid={2:X4} pid={3:X4} page={4:X4} usage={5:X4} {6}", d.Handle, d.Type, Marshal.ReadInt32(p, 8), Marshal.ReadInt32(p, 12), Marshal.ReadInt16(p, 20), Marshal.ReadInt16(p, 22), name);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }
    }
}