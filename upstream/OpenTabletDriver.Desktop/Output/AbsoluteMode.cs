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

        [Resolved]
        public override IAbsolutePointer Pointer { set; get; }
    }
}