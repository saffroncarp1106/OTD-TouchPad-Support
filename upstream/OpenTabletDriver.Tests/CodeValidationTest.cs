using System;
using System.Linq;
using System.Reflection;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace OpenTabletDriver.Tests
{
    public static class CodeValidationTestData
    {
        /// <summary>
        /// Namespaces known to contain types with report parsers
        /// </summary>
        private static readonly Assembly[] s_AssembliesWithReportParsers =
        [
            Assembly.Load("OpenTabletDriver.Configurations"),
            Assembly.Load("OpenTabletDriver.Plugin"),
        ];

        /// <summary>
        /// Types inheriting from <see cref="IDeviceReport"/>
        /// </summary>
        public static TheoryData<Type> IDeviceReportTypes
        {
            get
            {
                var result = new TheoryData<Type>();

                foreach (var assembly in s_AssembliesWithReportParsers)
                    result.AddRange([.. assembly.ExportedTypes.Where(TypeIsIDeviceReport)]);

                return result;
            }
        }

        private static bool TypeIsIDeviceReport(Type type) =>
            type is { IsInterface: false, IsAbstract: false } && type.IsAssignableTo(typeof(IDeviceReport));
    }

    public class CodeValidationTest
    {
        /// <summary>
        /// Ensure that report parsers inheriting from <see cref="IDeviceReport"/> are always value types, for performance
        /// </summary>
        [Theory]
        [MemberData(nameof(CodeValidationTestData.IDeviceReportTypes), MemberType = typeof(CodeValidationTestData))]
        public void ReportParsers_Are_Structs(Type type) => Assert.True(type.IsValueType);
    }
}
