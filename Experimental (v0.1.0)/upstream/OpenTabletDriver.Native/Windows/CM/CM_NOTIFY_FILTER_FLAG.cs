using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.CM
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum CM_NOTIFY_FILTER_FLAG
    {
        DEFAULT,
        ALL_INTERFACE_CLASSES,
        ALL_DEVICE_INSTANCES
    }
}
