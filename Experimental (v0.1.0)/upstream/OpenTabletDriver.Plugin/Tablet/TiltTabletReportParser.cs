using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Plugin.Tablet
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public class TiltTabletReportParser : IReportParser<IDeviceReport>
    {
        public virtual IDeviceReport Parse(byte[] data)
        {
            return new TiltTabletReport(data);
        }
    }
}
