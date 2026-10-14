using System.Runtime.InteropServices;
using System.Text.Json;
using OpenTabletDriver.Devices.LinuxTouchpad;
using OpenTabletDriver.Plugin.Tablet;
using static OpenTabletDriver.Devices.LinuxTouchpad.LinuxEvdev;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
ushort X(byte[] packet) => BitConverter.ToUInt16(packet, 2);
ushort Y(byte[] packet) => BitConverter.ToUInt16(packet, 4);
var x = new LinuxAxis(100, 1100, 10);
var y = new LinuxAxis(-100, 900, 10);
Check(x.Normalize(100) == 0 && x.Normalize(1100) == 65535 && x.Normalize(600) == 32768, "Axis normalization");
Check(x.Normalize(-1000) == 0 && x.Normalize(3000) == 65535, "Axis clamp");
Check(Math.Abs(x.Millimeters.Value - 100) < .001, "Axis millimeters");
Check(!new LinuxAxis(10,10,1).Valid && new LinuxAxis(0,100,0).Millimeters == null, "Missing/invalid axis metadata");
var encoder = new LinuxContactEncoder();
var packets = encoder.Encode(new[] { new LinuxContact(1,100,-100) }, x,y);
Check(packets.Length == 1 && X(packets[0]) == 0 && Y(packets[0]) == 0 && packets[0][0] == 1, "Top-left corner");
packets = encoder.Encode(new[] { new LinuxContact(1,1100,900) },x,y);
Check(X(packets[0]) == 65535 && Y(packets[0]) == 65535, "Bottom-right corner");
packets = encoder.Encode(new[] { new LinuxContact(2,600,400) },x,y);
Check(packets.Length == 2 && packets[0][0] == 0 && X(packets[1]) == 32768, "Tracking ID boundary release");
Check(encoder.Encode(new[] { new LinuxContact(2,600,400), new LinuxContact(3,0,0) },x,y).Single()[0] == 0, "Multiple contacts release");
Check(encoder.Encode(Array.Empty<LinuxContact>(),x,y).Length == 0, "Duplicate release suppression");
Check(encoder.Encode(new[] { new LinuxContact(3,100,-100) },x,y,true).Single()[5] == 255, "Y inversion");
Check(encoder.Encode(new[] { new LinuxContact(3,100,-100,Palm:true) },x,y).Single()[0] == 0, "Palm release");
encoder.Encode(new[] { new LinuxContact(4,100,-100) },x,y);
Check(encoder.Encode(new[] { new LinuxContact(4,100,-100,Complete:false) },x,y).Single()[0] == 0, "Incomplete contact release");
LinuxTouchpadControl.Acquire("test"); LinuxTouchpadControl.Acquire("test");
LinuxTouchpadControl.Release("test"); Check(LinuxTouchpadControl.Active("test"), "Claim lifetime across output replacement");
LinuxTouchpadControl.Release("test"); Check(!LinuxTouchpadControl.Active("test"), "Claim disposal");
Check(Marshal.SizeOf<InputEvent>() == (IntPtr.Size == 8 ? 24 : 16), "Linux input_event ABI");
Console.WriteLine("PASS: axis ranges/dimensions, corners, ID changes, lift, multiple contacts, palms, incomplete contacts, claims, native ABI");

if (args.Length > 0 && args[0] == "--inventory")
{
    LinuxTouchpadSelection.Refresh();
    Console.WriteLine(JsonSerializer.Serialize(LinuxTouchpadSelection.Configurations(), new JsonSerializerOptions { WriteIndented=true }));
    return;
}
if (args.Length > 0 && args[0] == "--integration")
{
    IntPtr fake = libevdev_new(), output = IntPtr.Zero;
    try
    {
        libevdev_set_name(fake, "OTD Test Touchpad");
        int Enable(uint type, uint code) => libevdev_enable_event_code(fake,type,code,IntPtr.Zero);
        void AxisCode(uint code,int min,int max,int resolution=10)
        {
            var info = new AbsInfo {Min=min,Max=max,Resolution=resolution};
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<AbsInfo>());
            try { Marshal.StructureToPtr(info,ptr,false); LinuxEvdev.Check(libevdev_enable_event_code(fake,EV_ABS,code,ptr), "Enable axis"); }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        LinuxEvdev.Check(libevdev_enable_property(fake,0),"Pointer property");
        foreach (uint code in new uint[] {325,330,333,272}) LinuxEvdev.Check(Enable(EV_KEY,code),"Enable contact keys");
        AxisCode(ABS_X,0,1000);AxisCode(ABS_Y,0,1000);
        AxisCode(ABS_MT_SLOT,0,1);AxisCode(ABS_MT_TRACKING_ID,0,65535);
        AxisCode(ABS_MT_POSITION_X,0,1000);AxisCode(ABS_MT_POSITION_Y,0,1000);
        LinuxEvdev.Check(libevdev_uinput_create_from_device(fake,-2,out output),"Create synthetic touchpad");
        string node = Text(libevdev_uinput_get_devnode(output));
        Thread.Sleep(300); // Allow udev/logind to apply the active-seat ACL.
        using var handle = new LinuxInputHandle(node);
        var endpoint = LinuxTouchpadEndpoint.Discover(node,handle.Device);
        Check(endpoint != null,"Synthetic touchpad discovery");
        using var stream = endpoint.Open();
        LinuxTouchpadControl.Acquire(endpoint.Identity);
        Thread.Sleep(150);
        void Send(uint type,uint code,int value) => LinuxEvdev.Check(libevdev_uinput_write_event(output,type,code,value),"Send test event");
        byte[] Read()
        {
            var task = Task.Run(stream.Read);
            if (!task.Wait(2000)) throw new Exception("Timed out waiting for encoded native report");
            return task.Result;
        }
        Send(EV_ABS,ABS_MT_SLOT,0);Send(EV_ABS,ABS_MT_TRACKING_ID,1);Send(EV_ABS,ABS_MT_POSITION_X,250);Send(EV_ABS,ABS_MT_POSITION_Y,750);
        Send(EV_KEY,BTN_TOUCH,1);Send(EV_KEY,BTN_TOOL_FINGER,1);Send(EV_SYN,0,0);
        var packet = Read(); Check(packet[0]==1 && X(packet)==16384 && Y(packet)==49151,"Native position frame");
        Send(EV_ABS,ABS_MT_POSITION_X,500);Send(EV_SYN,0,0);
        packet=Read();Check(X(packet)==32768 && Y(packet)==49151,"Native unchanged slot coordinates");
        Send(EV_ABS,ABS_MT_SLOT,1);Send(EV_ABS,ABS_MT_TRACKING_ID,2);Send(EV_ABS,ABS_MT_POSITION_X,900);Send(EV_ABS,ABS_MT_POSITION_Y,900);Send(EV_KEY,333,1);Send(EV_SYN,0,0);
        Check(Read()[0]==0,"Native multifinger suppression");
        Send(EV_ABS,ABS_MT_TRACKING_ID,-1);Send(EV_KEY,333,0);Send(EV_SYN,0,0);
        Check(Read()[0]==1,"Native single-finger resume");
        LinuxTouchpadControl.Release(endpoint.Identity);
        Check(Read()[0]==0,"Disable mapping releases contact");
        Thread.Sleep(100);
        LinuxEvdev.Check(libevdev_grab(handle.Device,3),"Verify hardware grab restored after disable");
        LinuxEvdev.Check(libevdev_grab(handle.Device,4),"Release test grab");
        LinuxTouchpadControl.Acquire(endpoint.Identity);
        Check(Read()[0]==1,"Enable resynchronizes already active finger");
        stream.Dispose();
        LinuxEvdev.Check(libevdev_grab(handle.Device,3),"Verify hardware grab restored after stream disposal");
        LinuxEvdev.Check(libevdev_grab(handle.Device,4),"Release test grab");
        LinuxTouchpadControl.Release(endpoint.Identity);
        Console.WriteLine("PASS: real uinput/evdev discovery, slot persistence, frame mapping, multifinger suppression, enable/disable resync, grab cleanup");
    }
    finally { if(output!=IntPtr.Zero)libevdev_uinput_destroy(output);libevdev_free(fake); }
    return;
}
if (args.Length == 0) return;
int frames=0, reports=0, releases=0, multi=0;
encoder = new LinuxContactEncoder();
LinuxAxis rawX=default, rawY=default;
foreach (var line in File.ReadLines(args[0]))
{
    using var doc=JsonDocument.Parse(line);var row=doc.RootElement;
    string kind=row.GetProperty("kind").GetString();
    if(kind=="start")
    {
        var device=row.GetProperty("device");var axes=device.GetProperty("axes");
        LinuxAxis Get(string code) {var a=axes.GetProperty(code);return new(a.GetProperty("min").GetInt32(),a.GetProperty("max").GetInt32(),a.GetProperty("resolution").GetInt32());}
        rawX=Get("53");rawY=Get("54");
        Console.WriteLine($"Replay {device.GetProperty("name").GetString()}: {rawX.Millimeters:F2} x {rawY.Millimeters:F2} mm");
    }
    if(kind!="frame")continue;
    frames++;
    var contacts=row.GetProperty("points").EnumerateArray().Select(p=>new LinuxContact(
        p.GetProperty("Id").GetInt32(),p.GetProperty("raw_x").GetInt32(),p.GetProperty("raw_y").GetInt32(),
        Palm:p.GetProperty("raw_axes").TryGetProperty("55",out var tool) && tool.GetInt32()==2)).ToList();
    if(row.GetProperty("count").GetInt32()!=contacts.Count) contacts.Add(new(-1,0,0,Complete:false));
    if(row.GetProperty("buttons").EnumerateArray().Any(k=>new[]{333,334,335,328}.Contains(k.GetInt32()))) contacts.Add(new(-1,0,0,Complete:false));
    if(contacts.Count>1)multi++;
    var encoded=encoder.Encode(contacts,rawX,rawY);
    foreach(var packet in encoded)
    {
        if(packet[0]==0){releases++;continue;}
        reports++;
        Check(contacts.Count==1,"Mapped more than one contact");
        var point=row.GetProperty("points")[0];
        Check(X(packet)==point.GetProperty("X").GetInt32() && Y(packet)==point.GetProperty("Y").GetInt32(),"Real capture normalized position mismatch");
    }
}
Check(frames>0 && reports>0,"No real capture reports replayed");
Console.WriteLine($"PASS: {frames} captured frames, {reports} position reports, {releases} releases, {multi} multifinger frames suppressed");
