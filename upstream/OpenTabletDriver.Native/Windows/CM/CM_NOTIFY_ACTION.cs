using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.CM
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum CM_NOTIFY_ACTION
    {
        DEVICEINTERFACEARRIVAL,
        DEVICEINTERFACEREMOVAL,
        DEVICEQUERYREMOVE,
        DEVICEQUERYREMOVEFAILED,
        DEVICEREMOVEPENDING,
        DEVICEREMOVECOMPLETE,
        DEVICECUSTOMEVENT,
        DEVICEINSTANCEENUMERATED,
        DEVICEINSTANCESTARTED,
        DEVICEINSTANCEREMOVED,
        MAX
    }
}
