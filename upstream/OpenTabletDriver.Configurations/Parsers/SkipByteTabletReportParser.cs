using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Configurations.Parsers
{
    public class SkipByteTabletReportParser : TabletReportParser
    {
        public override ITabletReport Parse(byte[] data)
        {
            return base.Parse(data[1..^0]);
        }
    }
}
