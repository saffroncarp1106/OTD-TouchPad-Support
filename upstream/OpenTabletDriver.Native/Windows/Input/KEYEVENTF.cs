using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.Input
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum KEYEVENTF : short
    {
        KEYDOWN = 0x0000,
        EXTENDEDKEY = 0x0001,
        KEYUP = 0X0002,
        UNICODE = 0x0004,
        SCANCODE = 0x0008
    }
}
