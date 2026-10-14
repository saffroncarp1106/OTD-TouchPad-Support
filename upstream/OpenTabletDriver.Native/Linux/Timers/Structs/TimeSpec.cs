using System.Runtime.InteropServices;

namespace OpenTabletDriver.Native.Linux.Timers.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    public record struct TimeSpec(long sec, long nsec)
    {
        public long sec = sec;
        public long nsec = nsec;
    }
}
