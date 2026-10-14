using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Eto.Forms;

namespace OpenTabletDriver.UX.Wpf
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--asus-daemon"))
            {
                OpenTabletDriver.Daemon.Program.RunEmbedded(Array.Empty<string>()).GetAwaiter().GetResult();
                return;
            }

            if (Process.GetProcessesByName("OpenTabletDriver.Daemon").Length > 0 || Process.GetProcessesByName("OpenTabletDriver.UX.Wpf").Length > 0 || Process.GetProcessesByName("ASUS-G15-Touchpad").Length > 0)
            {
                _ = new Application(Eto.Platforms.Wpf);
                MessageBox.Show("Another OpenTabletDriver copy is running. Quit it (including its system-tray icon), then open OTD Touchpad Experimental again.", "OpenTabletDriver already running", MessageBoxType.Information);
                return;
            }

            if (!TouchpadSetup.Ensure(args.Contains("--touchpad-setup")))
                return;
            args = args.Concat(new[] { "--skipupdate" }).Distinct().ToArray();
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                _ = new Application(Eto.Platforms.Wpf);
                MessageBox.Show("OpenTabletDriver should not be run with administrator privileges.\n" + "Some features may not work as intended, such as Plugin Manager and Tablet Debugger.\n" + "If you did not manually set OpenTabletDriver to run with administrator privileges please enable UAC to resolve this.\n" + "If it isn't resolved after, you are using the Administrator named account which overrides UAC.", MessageBoxType.Warning);
                Eto.Platform.AllowReinitialize = true;
            }

            App.Run(Eto.Platforms.Wpf, args);
        }
    }
}