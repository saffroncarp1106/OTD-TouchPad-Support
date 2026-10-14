using System;
using System.Threading.Tasks;
using OpenTabletDriver.Devices.AsusTouchpad;

static byte[] Contact(int x) => new byte[]
{
    1,
    (byte)x
};
static void Check(bool ok, string reason)
{
    if (!ok)
        throw new Exception(reason);
}

using (var queue = new LatestTouchpadReports())
{
    for (int i = 0; i < 100; i++)
        queue.Enqueue(Contact(i));
    queue.Enqueue(new byte[] { 0, 0 });
    queue.Enqueue(Contact(100));
    queue.Enqueue(Contact(101));
    Check(queue.Read()[1] == 99, "Old movement was retained");
    Check(queue.Read()[0] == 0, "Release was lost");
    Check(queue.Read()[1] == 101, "New contact was lost");
    var waiting = Task.Run(() => queue.Read());
    queue.Enqueue(Contact(102));
    Check(waiting.Wait(2000) && waiting.Result[1] == 102, "Reader did not wake for input");
}

var closing = new LatestTouchpadReports();
var reader = Task.Run(() =>
{
    try
    {
        closing.Read();
        return false;
    }
    catch (ObjectDisposedException)
    {
        return true;
    }
});
closing.Dispose();
Check(reader.Wait(2000) && reader.Result, "Shutdown did not release reader");
closing.Enqueue(Contact(1));
Console.WriteLine("PASS: latest movement, contact transitions, reader wakeup, and shutdown");