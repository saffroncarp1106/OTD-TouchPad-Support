using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenTabletDriver.Devices.AsusTouchpad
{
    internal sealed class LatestTouchpadReports : IDisposable
    {
        private readonly object sync = new();
        private readonly LinkedList<byte[]> pending = new();
        private bool disposed;
        public void Enqueue(byte[] report)
        {
            lock (sync)
            {
                if (disposed)
                    return;
                if (pending.Last != null && pending.Last.Value[0] == report[0])
                    pending.Last.Value = report;
                else
                    pending.AddLast(report);
                Monitor.Pulse(sync);
            }
        }

        public byte[] Read()
        {
            lock (sync)
            {
                while (pending.Count == 0 && !disposed)
                    Monitor.Wait(sync);
                if (disposed)
                    throw new ObjectDisposedException(nameof(LatestTouchpadReports));
                var report = pending.First.Value;
                pending.RemoveFirst();
                return report;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                pending.Clear();
                Monitor.PulseAll(sync);
            }
        }
    }
}