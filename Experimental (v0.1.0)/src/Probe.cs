using AsusTouchpad;
using System.Text.RegularExpressions;

var devices = TouchpadNative.Enumerate();
Console.WriteLine($"Matching touchpads: {devices.Count}");
foreach (var device in devices)
    Console.WriteLine(device);
if (devices.Count == 0)
    return 2;
if (args.Length > 1 && args[0] == "--verify")
{
    int compared = 0;
    var device = devices[0];
    byte[]? sample = null;
    foreach (string line in File.ReadLines(args[1]))
    {
        var match = Regex.Match(line, @"^\S+ ([0-9A-F]+) TouchPoint \{ Id = (\d+), X = (\d+), Y = (\d+) \}");
        if (!match.Success)
            continue;
        byte[] raw = Convert.FromHexString(match.Groups[1].Value);
        sample = raw;
        var points = TouchpadNative.Parse(device, raw);
        if (points.Count != 1 || points[0].Id != uint.Parse(match.Groups[2].Value) || points[0].X != ushort.Parse(match.Groups[3].Value) || points[0].Y != ushort.Parse(match.Groups[4].Value))
            throw new Exception("Replay mismatch");
        if (points[0].X != TouchpadNative.Normalize(BitConverter.ToUInt16(raw, 2), 0, 3990) || points[0].Y != TouchpadNative.Normalize(BitConverter.ToUInt16(raw, 4), 0, 2578))
            throw new Exception("Coordinate mismatch");
        if (TouchpadNative.ContactCount(device, raw) != raw[8])
            throw new Exception("Contact count mismatch");
        compared++;
    }

    if (compared == 0 || sample == null)
        throw new Exception("No captured reports to test");
    var up = (byte[])sample.Clone();
    up[1] &= 0xFD;
    if (TouchpadNative.Parse(device, up).Count != 0)
        throw new Exception("Finger-up is not rejected");
    var palm = (byte[])sample.Clone();
    palm[1] &= 0xFE;
    if (TouchpadNative.Parse(device, palm).Count != 0)
        throw new Exception("Palm is not rejected");
    if (TouchpadNative.Parse(device, new byte[] { 4, 3 }).Count != 0)
        throw new Exception("Truncated report is not rejected");
    if (TouchpadNative.Normalize(0, 0, 3990) != 0 || TouchpadNative.Normalize(3990, 0, 3990) != 65535 || TouchpadNative.Normalize(5000, 0, 3990) != 65535)
        throw new Exception("Edge mapping failed");
    Console.WriteLine($"PASS: {compared} captured reports match; contact counts, finger-up, palm rejection, truncated input, and edge mapping checks pass.");
}

if (args.Contains("--capture"))
{
    using var capture = new TouchpadReceiver(devices[0]);
    int reports = 0, contacts = 0;
    capture.Error += ex => Console.WriteLine(ex);
    capture.Report += (raw, points) =>
    {
        Interlocked.Increment(ref reports);
        if (points.Count > 0)
            Interlocked.Increment(ref contacts);
        Console.WriteLine($"{DateTime.UtcNow:O} {Convert.ToHexString(raw)} {string.Join(" ", points)}");
    };
    Console.WriteLine("Capture only: touch each corner and lift your finger. No pointer changes. Listening 30 seconds.");
    Thread.Sleep(30000);
    Console.WriteLine($"Reports={reports}; reports with confident contacts={contacts}");
}

foreach (var device in devices)
    device.Dispose();
return 0;