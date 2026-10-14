using System;
using System.Reflection;
using Eto.Forms;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Desktop.ViewModels;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.UX.Windows.Tablet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        _ = new Application(Eto.Platforms.Wpf);
        using var label = new Label();
        var bind = typeof(TabletDebugger).GetMethod("BindDeviceName", BindingFlags.NonPublic | BindingFlags.Static)!;
        bind.Invoke(null, new object[] { label });
        Check(label.Text == string.Empty, "Null data context must not crash initialization");
        using var vm = new TabletDebuggerViewModel();
        label.DataContext = vm;
        Check(label.Text == string.Empty, "No reports must show an empty label");
        foreach (var (name, expected) in new[] {
            ("ASUS Precision Touchpad [04F3:319F] (Absolute)", "ASUS P..."),
            ("123456", "123456"), ("1234567", "123456..."), ("Wacom", "Wacom"), ("", "") })
        {
            var report = new DebugReportData { Tablet = new TabletReference { Properties = new TabletConfiguration { Name = name } } };
            typeof(TabletDebuggerViewModel).GetField("_reportData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, report);
            typeof(ViewModel).GetMethod("RaiseChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vm, new object[] { "DeviceName" });
            Check(label.Text == expected, $"Expected '{expected}', got '{label.Text}'");
            Check(label.ToolTip == name, "Full device name must remain in tooltip");
        }
        label.DataContext = null;
        Check(label.Text == string.Empty, "Clearing data context must be safe");
        Console.WriteLine("PASS: real WPF label binding handles null initialization, live name updates, six-character truncation, tooltip, and context removal.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
