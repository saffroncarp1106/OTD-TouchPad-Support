using System.Diagnostics.CodeAnalysis;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Pointer;

namespace OpenTabletDriver.Desktop.Output
{
    [PluginName("Absolute Mode")]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public class AbsoluteMode : AbsoluteOutputMode
    {
        protected override void OnOutput(OpenTabletDriver.Plugin.Tablet.IDeviceReport report)
        {
            if (Tablet?.Properties.Attributes?.ContainsKey("PrecisionTouchpad") == true && report is OpenTabletDriver.Plugin.Tablet.ITabletReport)
            {
                if (!AsusTouchpad.TouchpadControl.Enabled)
                    return;
                AsusTouchpad.TouchpadControl.NotifyAbsoluteOutput();
            }

            base.OnOutput(report);
        }

        private string linuxIdentity;

        public override OpenTabletDriver.Plugin.Tablet.TabletReference Tablet
        {
            get => base.Tablet;
            set
            {
                if (linuxIdentity != null)
                    OpenTabletDriver.Devices.LinuxTouchpad.LinuxTouchpadControl.Release(linuxIdentity);
                linuxIdentity = null;
                base.Tablet = value;
                if (System.OperatingSystem.IsLinux() &&
                    value?.Properties.Attributes?.TryGetValue("LinuxTouchpadIdentity", out var identity) == true &&
                    Pointer is OpenTabletDriver.Desktop.Interop.Input.Absolute.EvdevAbsolutePointer pointer && pointer.CanWrite)
                {
                    linuxIdentity = identity;
                    OpenTabletDriver.Devices.LinuxTouchpad.LinuxTouchpadControl.Acquire(identity);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (linuxIdentity != null)
            {
                OpenTabletDriver.Devices.LinuxTouchpad.LinuxTouchpadControl.Release(linuxIdentity);
                linuxIdentity = null;
            }
            base.Dispose(disposing);
        }

        [Resolved]
        public override IAbsolutePointer Pointer { set; get; }
    }
}