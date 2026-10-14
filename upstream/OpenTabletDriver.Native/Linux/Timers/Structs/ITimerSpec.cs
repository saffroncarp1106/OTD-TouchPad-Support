using System.Runtime.InteropServices;

namespace OpenTabletDriver.Native.Linux.Timers.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    public record struct ITimerSpec(TimeSpec it_interval, TimeSpec it_value)
    {
        public TimeSpec it_interval = it_interval;
        public TimeSpec it_value = it_value;
    }
}
