using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Configurations.Parsers.ASUS
{
    public sealed class AsusTouchpadReportParser : IReportParser<IDeviceReport>
    {
        public IDeviceReport Parse(byte[] data) => data.Length == 9 && data[0] == 1 ? new TabletReport(data) : new OutOfRangeReport(data);
    }
}