using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum XBUTTON : uint
    {
        NONE = 0x0000,
        XBUTTON1 = 0x0001,
        XBUTTON2 = 0x0002
    }
}
