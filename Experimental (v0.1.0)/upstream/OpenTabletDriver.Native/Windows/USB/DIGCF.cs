using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.USB
{
    [Flags]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum DIGCF
    {
        None = 0,
        Default = 1,
        Present = 2,
        AllClasses = 4,
        Profile = 8,
        DeviceInterface = 16
    }
}
