using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.CM
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum CM_NOTIFY_FILTER_TYPE
    {
        DEVICEINTERFACE = 0,
        DEVICEHANDLE,
        DEVICEINSTANCE,
        MAX
    }
}
