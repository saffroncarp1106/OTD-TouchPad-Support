using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Linux.Timers
{
    /// <summary>
    /// The clock type to follow. See <c>clock_getres(2)</c> for details.
    /// </summary>
    /// <remarks>
    /// Only some of these enums are valid for <c>timerfd_create</c>, the extern used for <see cref="Timers.TimerCreate"/>
    /// </remarks>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum ClockID
    {
        /// <summary>
        /// Based on system clock, following system time changes, including administrator changing date and time
        /// </summary>
        RealTime = 0,
        /// <summary>
        /// Monotonically increasing, ignoring administrator system time changes, but follows <c>adjtime(3)</c> and NTP
        /// </summary>
        Monotonic = 1,
        /// <summary>
        /// Clock measuring CPU time by all threads in the process
        /// </summary>
        ProcessCPUTimeID = 2,
        /// <summary>
        /// Clock measuring CPU time by the thread associated with the timer
        /// </summary>
        ThreadCPUTimeID = 3,
        /// <summary>
        /// A clock that is monotonically increasing, completely independent of system clock.
        /// Ideal for non-wall clock hardware timers (like clock crystals).
        /// </summary>
        /// <remarks>Not supported with <c>timerfd_create</c></remarks>
        MonotonicRaw = 4,
        [Obsolete($"Typo, use {nameof(RealTimeCoarse)} instead")]
        RealTimeCourse = RealTimeCoarse,
        /// <summary>
        /// A faster but less precise version of <see cref="RealTime"/>
        /// </summary>
        RealTimeCoarse = 5,
        [Obsolete($"Typo, use {nameof(MonotonicCoarse)} instead")]
        MonotonicCourse = MonotonicCoarse,
        /// <summary>
        /// A faster but less precise version of <see cref="Monotonic"/>
        /// </summary>
        MonotonicCoarse = 6,
        /// <summary>
        /// Same as <see cref="RealTime"/> but also includes time while suspended
        /// </summary>
        BootTime = 7,
        /// <summary>
        /// Like <see cref="RealTime"/> but will wake system if suspended. Requires <c>CAP_WAKE_ALARM</c> capability.
        /// </summary>
        RealTimeAlarm = 8,
        /// <summary>
        /// Like <see cref="BootTime"/> but will wake system if suspended. Requires <c>CAP_WAKE_ALARM</c> capability.
        /// </summary>
        BootTimeAlarm = 9,
        [Obsolete("Removed upstream")]
        SGICycle = 10,
        /// <summary>
        /// Like <see cref="RealTime"/> but does not experience discontinuities and
        /// backwards jumps caused by NTP inserting leap seconds
        /// </summary>
        TAI = 11
    }
}
