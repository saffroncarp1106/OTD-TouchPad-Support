using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace OpenTabletDriver.Plugin.Tablet
{
    /// <summary>
    /// Device specifications for an analog reporting device such as strips
    /// </summary>
    public class AnalogSpecifications
    {
        /// <summary>
        /// The amount of steps in the analog device
        /// </summary>
        [JsonProperty, Required(ErrorMessage = $"{nameof(StepCount)} must be defined")]
        public uint StepCount { set; get; }

        /// <summary>
        /// Does the device report relative position (deltas) or absolute position (exact value)
        /// </summary>
        [JsonProperty, Required(ErrorMessage = $"{nameof(IsRelative)} must be defined")]
        public bool IsRelative { get; set; }

        public override string ToString()
        {
            var analogType = IsRelative ? "Relative" : "Absolute";
            return $"{StepCount} {analogType} Steps";
        }
    }
}
