using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.USB
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RequestInternalType : byte
    {
        Standard,
        Class,
        Vendor
    }
}
