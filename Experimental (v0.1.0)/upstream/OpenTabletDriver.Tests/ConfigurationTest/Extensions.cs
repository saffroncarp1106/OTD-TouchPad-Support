using System.Collections.Generic;
using Xunit;

namespace OpenTabletDriver.Tests.ConfigurationTest
{
    public static class Extensions
    {
        public static TheoryData<T> ToTheoryData<T>(this IEnumerable<T> enumerable)
        {
            var result = new TheoryData<T>();
            foreach (var element in enumerable)
                result.Add(element);
            return result;
        }
    }
}
