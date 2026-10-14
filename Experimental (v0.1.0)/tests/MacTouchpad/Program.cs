using System.Runtime.InteropServices;
using System.Text.Json;
using OpenTabletDriver.Devices.AsusTouchpad;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
MacContact Point(float x, float y, int state = 4, int id = 1) => new() { State = state, Identifier = id, Normalized = new() { Position = new() { X = x, Y = y } } };
Check(Marshal.SizeOf<MacContact>() == 96, "ABI size");
Check(Marshal.OffsetOf<MacContact>(nameof(MacContact.Normalized)).ToInt32() == 32, "ABI position offset");
var encoder = new MacContactEncoder();
var packet = encoder.Encode(new[] { Point(0, 0) }).Single();
Check(packet[2] == 0 && packet[3] == 0 && packet[4] == 255 && packet[5] == 255, "Bottom left");
packet = encoder.Encode(new[] { Point(1, 1) }).Single();
Check(packet[2] == 255 && packet[3] == 255 && packet[4] == 0 && packet[5] == 0, "Top right");
Check(encoder.Encode(new[] { Point(.5f, .5f, 5) }).Single()[0] == 0, "Lift must release");
Check(encoder.Encode(Array.Empty<MacContact>()).Length == 0, "Repeated release");
encoder.Encode(new[] { Point(.2f, .3f) });
Check(encoder.Encode(new[] { Point(.2f, .3f, id: 2) }).Length == 2, "ID transition");
Check(encoder.Encode(new[] { Point(.2f, .3f), Point(.6f, .7f, id: 2) }).Single()[0] == 0, "Multiple fingers must release");
Check(encoder.Encode(new[] { Point(float.NaN, .3f) }).Length == 0, "Invalid coordinates");
if (args.Length > 0)
{
    using var capture = JsonDocument.Parse(File.ReadAllText(args[0]));
    int frames = 0, positions = 0, releases = 0;
    encoder = new();
    foreach (var frame in capture.RootElement.GetProperty("frames").EnumerateArray())
    {
        var contacts = frame.GetProperty("contacts").EnumerateArray().Select(p => Point(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle(), p.GetProperty("state").GetInt32(), p.GetProperty("id").GetInt32())).ToArray();
        foreach (var report in encoder.Encode(contacts))
        {
            Check(report.Length == 9, "Report size");
            if (report[0] == 1) positions++; else releases++;
        }
        frames++;
    }
    Check(positions > 0 && releases > 0, "Capture needs positions and releases");
    Console.WriteLine($"PASS: replayed {frames} frames; {positions} positions, {releases} releases.");
}
Console.WriteLine("PASS: ABI, corners, Y flip, lift, contact changes, multi-contact rejection, invalid coordinates.");
