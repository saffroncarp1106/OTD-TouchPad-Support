using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OpenTabletDriver.Native.Windows.Input
{
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public INPUT_TYPE type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf(typeof(INPUT));
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum INPUT_TYPE
    {
        MOUSE_INPUT,
        KEYBD_INPUT,
        HARDWARE_INPUT
    }
}
