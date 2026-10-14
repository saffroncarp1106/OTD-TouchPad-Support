namespace OpenTabletDriver.Tests.ConfigurationTest
{
    /// <summary>
    /// Attribute values for the test disablement key. These should be serialized as a comma-separated string
    /// </summary>
    public enum TestTypes
    {
        LPI_DIGITIZER_X, // Lines per inches/mm test for X axis on digitizer
        LPI_DIGITIZER_Y, // Lines per inches/mm test for Y axis on digitizer
        LPI_SAME_ACROSS_AXES, // Test that LPI matches across both dimensions
    }
}
