using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Windows.USB
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RequestRecipient : byte
    {
        Device,
        Interface,
        Endpoint,
        Other,
        VendorDefined = 0b0001_1111
    }
}
