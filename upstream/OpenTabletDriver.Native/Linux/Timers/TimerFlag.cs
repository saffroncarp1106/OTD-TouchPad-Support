using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Linux.Timers
{
    /// <summary>
    /// Flags associated with Linux timer API, see <c>timerfd_create(2)</c> and <c>read(2)</c>
    /// </summary>
    [Flags]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum TimerFlag
    {
        /// <summary>
        /// Default behavior
        /// </summary>
        Default = 0,
        /// <summary>
        /// Interpret the attached new <see cref="Structs.ITimerSpec.it_value"/> as an absolute value on the timer's clock
        /// </summary>
        AbsoluteTime = 1 << 0,
        /// <summary>
        /// Will signal <see cref="ERRNO.ECANCELED"/> on read if <see cref="AbsoluteTime"/> flags is also set,
        /// and clock ID <see cref="ClockID.RealTime"/> is used.
        /// </summary>
        CancelOnSet = 1 << 1,
        /// <summary>
        /// Will not block reads and instead return <see cref="ERRNO.EAGAIN"/> if timer hasn't lapsed
        /// </summary>
        /// <remarks>Most likely not valid for <see cref="Timers.TimerSetTime"/></remarks>
        NonBlocking = 1 << 14,
    }
}
