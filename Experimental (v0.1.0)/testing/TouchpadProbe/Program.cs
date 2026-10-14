using AsusTouchpad;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Buffers.Binary;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
static string? Arg(string name)
{
    int i = Array.IndexOf(Environment.GetCommandLineArgs(), name);
    return i >= 0 ? Environment.GetCommandLineArgs().ElementAtOrDefault(i + 1) : null;
}

if (args.Contains("--self-test"))
{
    var assembler = new ContactFrames();
    var a = new TouchPoint(1, 100, 200);
    var b = new TouchPoint(2, 300, 400);
    Check(assembler.Push(2, 10, new() { (a, true) }) == null, "Serial frame emitted early");
    Check(assembler.Push(0, 10, new() { (b, true) })?.Points.Count == 2, "Serial continuation lost");
    Check(assembler.Push(1, 11, new() { (b, true), (a, false) })?.Points.Single().Id == 2, "Surface slot selection");
    Check(assembler.Push(0, 12, new())?.Points.Count == 0, "Release lost");
    Check(assembler.Push(2, 13, new() { (a, true), (b, false) })?.Points.Count == 1, "Inactive contact leaked");
    Check(assembler.Push(2, 14, new() { (a, true) }) == null, "Hybrid frame emitted early");
    Check(assembler.Push(0, 15, new() { (b, true) })?.Points.Count == 0, "Mixed scan frames accepted");
    Check(assembler.Push(2, 16, new() { (a, true), (a, true) })?.Points.Count == 0, "Duplicate contact IDs accepted");
    var signed = new TouchpadDevice.Axis(1, 0x30, -100, 100, 0, 0, 0, 0, 4, 16);
    Check(TouchpadNative.NormalizeAxis(65436, signed) == 0, "Signed normalization");
    Check(TouchpadNative.NormalizeAxis(100, signed) == 65535, "Maximum normalization");
    using var surface = new TouchpadDevice
    {
        Path = "surface",
        Vendor = 0x45e,
        Product = 0x9af,
        Signature = "surface-v1"
    };
    surface.Axes.Add(new(1, 0x30, 0, 2227, 0, 1113, 17, -2, 4));
    surface.Axes.Add(new(1, 0x31, 0, 1459, 0, 729, 17, -2, 4));
    Check(Math.Abs(surface.Width!.Value - 111.3) < 0.01 && Math.Abs(surface.Height!.Value - 72.9) < 0.01, "Surface units");
    using var asus = new TouchpadDevice
    {
        Path = "asus",
        Vendor = 0x4f3,
        Product = 0x319f,
        Signature = "asus-v1"
    };
    var profile = new TouchpadProfile
    {
        Path = "surface",
        Vendor = surface.Vendor,
        Product = surface.Product,
        Signature = surface.Signature
    };
    Check(TouchpadProfile.Resolve(new[] { surface, asus }, null) == null, "Ambiguous autoselection");
    Check(TouchpadProfile.Resolve(new[] { surface, asus }, profile) == surface, "Saved selection");
    surface.Signature = "changed";
    Check(TouchpadProfile.Resolve(new[] { surface, asus }, profile) == null, "Changed descriptor accepted");
    Console.WriteLine("PASS: serial/hybrid frames, release, confidence filtering, duplicate IDs, signed axes, Surface units, and profile selection");
    return;
}

var devices = TouchpadNative.Enumerate();
try
{
    if (args.Contains("--list"))
    {
        Emit(new { kind = "devices", devices = devices.Select((d, i) => new { index = i, name = d.Name, path = d.Path, vendor = d.Vendor.ToString("X4"), product = d.Product.ToString("X4"), signature = d.Signature, width_mm = d.Width, height_mm = d.Height, input_bytes = d.InputLength, axes = d.Axes, fields = d.Fields }) });
        return;
    }

    int index = Arg("--device")is string selection ? int.Parse(selection) : devices.Count == 1 ? 0 : -1;
    if (index < 0 || index >= devices.Count)
        throw new InvalidOperationException("Run --list, then specify --device INDEX; automatic selection requires exactly one device.");
    var device = devices[index];
    if (Arg("--replay")is string replay)
    {
        int checkedCount = 0;
        foreach (var line in File.ReadLines(replay))
        {
            var m = Regex.Match(line, @"Z ([0-9A-F]+) TouchPoint");
            if (!m.Success)
                continue;
            var raw = Convert.FromHexString(m.Groups[1].Value);
            var frame = TouchpadNative.Decode(device, raw);
            Check(frame != null && frame.Points.Count == 1, "Capture did not decode as a single contact");
            var x = device.Axes.First(a => a.Usage == 0x30);
            var y = device.Axes.First(a => a.Usage == 0x31);
            Check(frame!.Points[0].X == TouchpadNative.Normalize(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2)), x.Min, x.Max), "Replay X mismatch");
            Check(frame.Points[0].Y == TouchpadNative.Normalize(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(4)), y.Min, y.Max), "Replay Y mismatch");
            checkedCount++;
        }

        Check(checkedCount > 0, "No capture records found");
        Console.WriteLine($"PASS: {checkedCount} real ASUS contacts decoded by generic backend");
        return;
    }

    double seconds = Arg("--seconds")is string duration ? double.Parse(duration, System.Globalization.CultureInfo.InvariantCulture) : 30;
    if (!double.IsFinite(seconds) || seconds <= 0 || seconds > 3600)
        throw new ArgumentException("Seconds must be between 0 and 3600");
    Emit(new { kind = "start", device = device.Name, path = device.Path, seconds, coordinates = "normalized 0..65535", note = "read-only capture; no mouse hooks or pointer output" });
    using var stop = new ManualResetEventSlim();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Set();
    };
    var clock = Stopwatch.StartNew();
    int frames = 0;
    using (var receiver = new TouchpadReceiver(device))
    {
        receiver.Frame += frame =>
        {
            Interlocked.Increment(ref frames);
            Emit(new { kind = "frame", seconds = clock.Elapsed.TotalSeconds, scan = frame.Scan, count = frame.ContactCount, points = frame.Points });
        };
        receiver.Error += ex => Emit(new { kind = "error", message = ex.Message });
        receiver.Removed += () =>
        {
            Emit(new { kind = "removed" });
            stop.Set();
        };
        stop.Wait(TimeSpan.FromSeconds(seconds));
    }

    Emit(new { kind = "end", frames, seconds = clock.Elapsed.TotalSeconds });
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    foreach (var device in devices)
        device.Dispose();
}