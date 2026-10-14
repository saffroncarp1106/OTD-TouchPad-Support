using System.Runtime.InteropServices;

namespace OpenTabletDriver.Native.OSX.Generic
{
    [StructLayout(LayoutKind.Sequential)]
    public record struct CGRect(CGPoint origin, CGSize size)
    {
        public CGPoint origin = origin;
        public CGSize size = size;
    }
}
