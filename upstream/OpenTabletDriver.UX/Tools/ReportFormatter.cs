using System;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.UX.Tools
{
    [Obsolete($"Moved to {nameof(Plugin.ReportFormatter)}")]
    public static class ReportFormatter
    {
        [Obsolete($"Moved to {nameof(Plugin.ReportFormatter)}")]
        public static string GetStringRaw(IDeviceReport report)
            => Plugin.ReportFormatter.GetStringRaw(report);

        [Obsolete($"Moved to {nameof(Plugin.ReportFormatter)}")]
        public static string GetStringRawAsBinary(IDeviceReport report)
            => Plugin.ReportFormatter.GetStringRawAsBinary(report);

        [Obsolete($"Moved to {nameof(Plugin.ReportFormatter)}")]
        public static string GetStringFormat(IDeviceReport report)
            => Plugin.ReportFormatter.GetStringFormat(report);

        [Obsolete($"Moved to {nameof(Plugin.ReportFormatter)}")]
        public static string GetStringFormatOneLine(TabletConfiguration tabletProperties, IDeviceReport report, TimeSpan delta, string reportType)
            => Plugin.ReportFormatter.GetStringFormatOneLine(tabletProperties, report, delta, reportType);
    }
}
