using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BenchmarkDotNet.Attributes;
using OpenTabletDriver.SystemDrivers;

namespace OpenTabletDriver.Benchmarks.Misc
{
    public class DriverInfoBenchmark
    {
        [Benchmark]
        [SuppressMessage("Performance", "CA1822:Mark members as static")] // invalid for benchmarks
        public DriverInfo[] GetDriverInfos()
        {
            return DriverInfo.GetDriverInfos().ToArray();
        }
    }
}
