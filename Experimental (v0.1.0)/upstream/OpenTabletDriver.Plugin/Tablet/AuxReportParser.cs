using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Plugin.Tablet
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public class AuxReportParser : IReportParser<IAuxReport>
    {
        public IAuxReport Parse(byte[] data)
        {
            return new AuxReport(data);
        }
    }
}
